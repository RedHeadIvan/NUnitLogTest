using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FSManager.State;

namespace FSManager.HostRepresentative
{
    /// <summary>
    /// Класс, иммитирующий поведение хоста
    /// </summary>
    public class HostRepresentative
    {
        private static HostRepresentative instance;

        public static HostRepresentative getInstance()
        {
            if (instance == null)
                instance = new HostRepresentative();
            return instance;
        }

        public SettingsPreprocessor preprocessor;

        public event EventHandler<(string ID, string Value)> GUI_Value_ChangedHW;

        public void OnGUI_Value_Changed((string ID, string Value) changedProperty)
        {
            GUI_Value_ChangedHW?.Invoke(null, changedProperty);
        }

        public event EventHandler<string> Get_HW_Mapping;

        public void OnGet_HW_Mapping(string data)
        {
            Get_HW_Mapping?.Invoke(null, data);
        }

        public bool settingsCorrected = false;

        bool isInited = false;   // HACK Так делать не стоит, наверное

        public void Init()
        {
            if (isInited)
                return;

            preprocessor.PropertyChanged_GP += SomeGUIValueChanged;
            preprocessor.preprocessingChangesReady += Preprocessor_preprocessingChangesReady;
            GUI_Value_ChangedHW += preprocessor.HostRepresentative_GUI_Value_Changed_HW;
            isInited = true;
        }

        private Dictionary<string, bool> changedGUIElements = new Dictionary<string, bool>();
        public event EventHandler<Dictionary<string, bool>> GUICorrectionReady;
        private void PathProcessor_GuiElementStateChanged(object sender, (string, bool) e)
        {
            if (!changedGUIElements.TryAdd(e.Item1, e.Item2))
            {
                changedGUIElements[e.Item1] = e.Item2;
            }
        }

        private void Preprocessor_preprocessingChangesRevert(object sender, EventArgs e)
        {
            changedProperties.Clear();
            changedGUIElements.Clear();
            settingsCorrected = false;
        }

        private Dictionary<string, string> changedProperties = new Dictionary<string, string>();
        public event EventHandler<Dictionary<string, string>> CorrectionReady;

        private void Preprocessor_preprocessingChangesReady(object sender, EventArgs e)
        {
            HostNotUpdating.Reset();
            if (changedProperties.Count > 0)
            {
                foreach (KeyValuePair<string, string> pair in changedProperties)
                {
                    Console.WriteLine($"{pair.Key} = {pair.Value}");
                }
                CorrectionReady?.Invoke(this, changedProperties);
                changedProperties.Clear();
            }
            if (changedGUIElements.Count > 0)
            {
                GUICorrectionReady?.Invoke(this, changedGUIElements);
                changedGUIElements.Clear();
            }
            settingsCorrected = false;
            HostNotUpdating.Set();
        }

        ManualResetEvent HostNotUpdating = new ManualResetEvent(true);
        AutoResetEvent doNotFuchingChangeMyDictionary = new AutoResetEvent(true);

        private void SomeGUIValueChanged(object sender, (string, string) e)
        {
            HostNotUpdating.WaitOne();
            if (!changedProperties.TryAdd(e.Item1, e.Item2))
            {
                changedProperties[e.Item1] = e.Item2;
            }
            settingsCorrected = true;
        }
    }
}
