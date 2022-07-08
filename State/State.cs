using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
//using GUK.CrateDevices.FPGA.Helpers;

[assembly: InternalsVisibleTo("UnitTests")]
namespace FSManager.State
{
    public class DataSourceParameterName : System.Attribute
    {
        public string Name { get; set; }

        public DataSourceParameterName()
        { }

        public DataSourceParameterName(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Класс, описывающий состояние системы
    /// </summary>
    public class State
    {
        private static State instance;

        public static State getInstance()
        {
            if (instance == null)
                instance = new State();
            return instance;
        }


        public ManualResetEvent scriptCompleted = new ManualResetEvent(false);

        public State()
        {

        }

        public State Clone()
        {
            return (State)this.MemberwiseClone();
        }

        /// <summary>
        /// Флаг, исключающий ситуацию, когда у нас начались какие-то внутренние пересчеты, а в это время снова изменились данные
        /// </summary>

        public ManualResetEvent HostDontMakeHisChanges = new ManualResetEvent(true);
        /// <summary>
        /// Флаг завершения обработки новых параметров
        /// </summary>

        public ManualResetEvent ChangesHandled = new ManualResetEvent(true);


        public SettingsPreprocessor SettingsPreprocessor_o;

        private EventHandler _SomeDataWasUpdated;

        /// <summary>
        /// Эвент для PathProcessor, инициирующий пересчет параметров
        /// </summary>
        public event EventHandler SomeDataWasUpdated
        {
            add
            {
                _SomeDataWasUpdated += value;

            }
            remove
            {
                _SomeDataWasUpdated -= value;
            }
        }

        public void OnSomeDataWasUpdated()
        {
            if (_SomeDataWasUpdated != null)
            {
                HostDontMakeHisChanges.WaitOne();
                _SomeDataWasUpdated(null, null);
            }
        }

        public bool lastCalcSucceed = true;

        /// <summary>
        /// Функция, вызываемая для обновления всех необходимых полей при смене настроек свипирования
        /// </summary>
        public void UpdateInternalValues(bool isevent)
        {
            UpdateRBW_60dB();
            ChangesHandled.Reset();
            if (isevent)
            {
                Task refreshTask = Task.Factory.StartNew(() =>
                {
                    OnSomeDataWasUpdated();
                    ChangesHandled.Set();
                });
            }
            else
            {
                ChangesHandled.Set();
            }
        }

        /// <summary>
        /// Функция, обновляющая ширину RBW по уровню -60 дБ в зависимости от пути
        /// </summary>
        private void UpdateRBW_60dB()
        {

        }



    }
}

