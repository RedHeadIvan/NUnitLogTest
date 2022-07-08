#define FSM_Log_Console
#define Other_Log_Console


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using NLog;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Binstate;

namespace FSManager.State
{
    /// <summary>
    /// Модуль, осуществляющий коррекцию настроек, поступающих в SetupScript
    /// </summary>
    public class SettingsPreprocessor : IDisposable
    {

        private static SettingsPreprocessor instance;

        public static SettingsPreprocessor getInstance()
        {
            if (instance == null)
                instance = new SettingsPreprocessor();
            return instance;
        }

        Logger Log;

        /// <summary>
        /// Объект SystemState, с которым взаимодействует препроцессор
        /// </summary>
        public State SystemState_o;

        /// <summary>
        /// Очередь Хардварных пропертей для обработки
        /// </summary>
        private ConcurrentQueue<(string ID, string Value)> guiChangeQueueHW = new ConcurrentQueue<(string ID, string Value)>();

        /// <summary>
        /// Лист сообщений для вывода в GUI
        /// </summary>
        private Dictionary<string, string> textmessagelist = new Dictionary<string, string>();

        /// <summary>
        /// Событие готовности результатов коррекции
        /// </summary>
        public event EventHandler preprocessingChangesReady;

        /// <summary>
        /// Событие пропуска коррекции свойства
        /// </summary>
        public event EventHandler preprocessingChangesInterupt;

        /// <summary>
        /// Событие, передающее сообщения в интерфейс
        /// </summary>
        public event EventHandler<Dictionary<string, string>> messagesReady;

        #region FSM

        /// <summary>
        /// Таймер, определяющий максимальное время выдачи результатов коррекции в интерфейс
        /// </summary>
        private System.Timers.Timer FSMTimer;

        /// <summary>
        /// Флаг окончания времени на пачку коррекции
        /// </summary>
        private AutoResetEvent FSMTimeout = new AutoResetEvent(false);

        private ManualResetEvent FreeToProcess = new ManualResetEvent(true);


        /// <summary>
        /// Обработка тика таймера FSM
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FSMTimeout_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            FreeToProcess.Reset();
#if FSM_Log_Console && DEBUG
            Console.WriteLine("FSM timer tick");
#endif
            FSMTimeout.Set();
        }

        /// <summary>
        /// FSM, реализующая логику состояний обработчика
        /// </summary>
        private IStateMachine<Events> _packHandler;

        /// <summary>
        /// Инициализация всего, что связано с FSM
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void BuildFSM()
        {
            FSMTimer = new System.Timers.Timer(200);
            FSMTimer.Elapsed += FSMTimeout_Elapsed;
            FSMTimer.AutoReset = false;

            var builder = new Builder<States, Events>((Exception ex) =>
            {
                Console.WriteLine(ex.Message);
                throw new Exception(ex.Message);
            });

            builder
             .DefineState(States.Waiting)
             .AddTransition(Events.GetPack, States.Pack, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetPack, Waiting -> FirstPack");
#endif
             })
             .AddTransition(Events.GetBulkStart, States.BulkConfig, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetBulkStart, Waiting -> BulkConfig");
#endif
             });

            builder
             .DefineState(States.BulkConfig)
             .OnExit(HandleCorrectionResults)
             .AddTransition(Events.GetBulkStop, States.Waiting, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetBulkStop, BulkConfig -> Waiting");
#endif
             });

            builder
             .DefineState(States.Pack)
             .OnEnter(() =>
             {
                 TimerStartAction();
             })
             .AddTransition(Events.GetTimerZeroQueue, States.Waiting, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetTimerZeroQueue, FirstPack -> Waiting");
#endif
                 HandleCorrectionResults();
             })
             .AddTransition(Events.GetTimerNonZeroQueue, States.Pack, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetTimerNonZeroQueue, Pack -> Pack");
#endif
                 HandleCorrectionResults();
             })
             .AddTransition(Events.GetBulkStart, States.BulkConfig, () =>
             {
#if FSM_Log_Console && DEBUG
                 Console.WriteLine("FSM: GetBulkStart, FirstPack -> BulkConfig");
#endif
             });


            _packHandler = builder.Build(States.Waiting);
        }

        Mutex TransitionMutex = new Mutex();

        private void TimerStartAction()
        {
            FSMTimer.Start();
        }

        private void GetPack()
        {
            TransitionMutex.WaitOne();
            FreeToProcess.Reset();
            _packHandler.Raise(Events.GetPack);
            FreeToProcess.Set();
            TransitionMutex.ReleaseMutex();
        }

        private void GetTimerZeroQueue()
        {
            TransitionMutex.WaitOne();
            FreeToProcess.Reset();
            _packHandler.Raise(Events.GetTimerZeroQueue);
            FreeToProcess.Set();
            TransitionMutex.ReleaseMutex();
        }

        private void GetTimerNonZeroQueue()
        {
            TransitionMutex.WaitOne();
            FreeToProcess.Reset();
            _packHandler.Raise(Events.GetTimerNonZeroQueue);
            FreeToProcess.Set();
            TransitionMutex.ReleaseMutex();
        }

        private void GetStartBulk()
        {
            TransitionMutex.WaitOne();
            FreeToProcess.Reset();
            _packHandler.Raise(Events.GetBulkStart);
            FreeToProcess.Set();
            TransitionMutex.ReleaseMutex();
        }

        private void GetStopBulk()
        {
            TransitionMutex.WaitOne();
            FreeToProcess.Reset();
            _packHandler.Raise(Events.GetBulkStop);
            FreeToProcess.Set();
            TransitionMutex.ReleaseMutex();
        }

        private enum States
        {
            Waiting,
            Pack,
            AnotherPack,
            BulkConfig
        }

        private enum Events
        {
            GetPack,
            GetTimerZeroQueue,
            GetTimerNonZeroQueue,
            GetBulkStart,
            GetBulkStop
        }

        #endregion


        public void HostRepresentative_GUI_Value_Changed_HW(object sender, (string ID, string Value) e)
        {
#if Other_Log_Console
            Console.WriteLine($"{e.ID} waiting for processing");
#endif
            //lock (this)
            {
                string val_str = e.Value.ToString();

#if ParseValueProperly
                bool get_value = false;
                try
                {
                    double val = (double)((dynamic)(e)).NeutralValue;
                    val_str = val.ToString();
                    get_value = true;
                }
                catch (Exception)
                {
                    val_str = e.Value.ToString();
                    get_value = false;
                }
                if (!get_value)
                {
                    try
                    {
                        bool val = (bool)((dynamic)e).Value.Value;
                        val_str = val.ToString();
                        get_value = true;
                    }
                    catch (Exception)
                    {
                        val_str = e.Value.ToString();
                        get_value = false;
                    }
                }
#endif
                //FreeToProcess.WaitOne();
                guiChangeQueueHW.Enqueue(e);

#if Other_Log_Console
                Console.WriteLine($"get {e.ID} with value {val_str}");
#endif
            }
        }


        private object locker = new object();

        public void Init()
        {
            BuildFSM();
            ChangesHandlerThread = new Thread(ChangesHandlerMethod);
            ChangesHandlerThread.Name = "ChangesHandlerThread";
        }
        public void Start()
        {
            ChangesHandlerThread.Start();
        }
        public void Dispose()
        {
            FSMTimer.Elapsed -= FSMTimeout_Elapsed;
            FSMTimer.Dispose();
        }

        public SettingsPreprocessor()
        {
            Log = LogManager.GetCurrentClassLogger();
        }

        AutoResetEvent GetTimeout = new AutoResetEvent(false);
        private void Timeout_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            GetTimeout.Set();
        }

        Thread ChangesHandlerThread;

        private void TryOneProperty((string ID, string Value) prop)
        {
            if (true)
            {
                bool applied = true;
                if (applied)
                {                    
                    SystemState_o.UpdateInternalValues(true);
                    SystemState_o.ChangesHandled.WaitOne();
                    if (!SystemState_o.lastCalcSucceed)
                    {

                    }
                    else
                    {
#if Other_Log_Console
                        Console.WriteLine($"Property {prop.ID} applied!");
#endif
                    }
                }
            }
        }

        public void ChangesHandlerMethod()
        {
            (string ID, string Value) prop;
            int i = 0;
            while (i < 200)
            {
                if (FreeToProcess.WaitOne(0))
                    if (guiChangeQueueHW.TryDequeue(out prop))
                    {
                        GetPack();
#if Other_Log_Console
                        Console.WriteLine($"Processing {prop.ID} with value { prop.Value}");
#endif
                        TryOneProperty(prop);
                        OnPropertyChanged_GP(prop.ID, prop.Value);
                    }

                if (FSMTimeout.WaitOne(0))
                {
                    if (guiChangeQueueHW.IsEmpty)
                        GetTimerZeroQueue();
                    else
                        GetTimerNonZeroQueue();
                }
                else
                {
                    Task.Delay(20).GetAwaiter().GetResult();
                }
                i++;
            }
        }
        private void HandleCorrectionResults()
        {
#if Other_Log_Console
            Console.WriteLine("Applying correction");
#endif
            bool have_corrected = true;

            if (have_corrected)
            {               
                CommitChanges();
            }

        }

        public void CommitChanges()
        {
            preprocessingChangesReady?.Invoke(this, null);
        }


#region События

        private EventHandler<(string, string)> _PropertyChanged_GP;
        public event EventHandler<(string, string)> PropertyChanged_GP
        {
            add
            {
                _PropertyChanged_GP += value;
            }
            remove
            {
                _PropertyChanged_GP -= value;
            }
        }
        public void OnPropertyChanged_GP(string name, string value)
        {
            if (_PropertyChanged_GP != null)
            {
                Console.WriteLine($"{name} corrected!");
                _PropertyChanged_GP(null, (name, value));
            }
        }

#endregion
    }

}

