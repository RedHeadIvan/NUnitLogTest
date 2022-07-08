#define DoubleStage

using FSManager.HostRepresentative;
using FSManager.State;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchedulerUnitTests
{
    [TestFixture]
    internal class PreprocessorTests
    {
        HostRepresentative hostRepresentative = HostRepresentative.getInstance();
        State state = State.getInstance();
        SettingsPreprocessor preprocessor = SettingsPreprocessor.getInstance();

#if DoubleStage
        [OneTimeSetUp]
        public void SetUp()
        {
            hostRepresentative = HostRepresentative.getInstance();
            state = State.getInstance();
            preprocessor = SettingsPreprocessor.getInstance();

            hostRepresentative.preprocessor = preprocessor;
            hostRepresentative.Init();

            state.UpdateInternalValues(false);
            state.ChangesHandled.WaitOne();
            preprocessor.SystemState_o = state;

            preprocessor.Init();
            preprocessor.Start();
        }

        [Test]
        public void SimpleHandMadeChain()
        {
            int timeout = 17;

            for (int i = 0; i < 3; i++)
            {

                hostRepresentative.OnGUI_Value_Changed(("CF", (200e6 + 1e6 * i).ToString()));

                Task.Delay(timeout).Wait();

                hostRepresentative.OnGUI_Value_Changed(("Span", (8e6 + 1e5 * i).ToString()));

                Task.Delay(timeout).Wait();

                //props.RBW.SetValue(90e3 + 1e3 * i);

                //hostRepresentative.OnGUI_Value_Changed(props.RBW.HardwareProperty);

                //Task.Delay(timeout).Wait();

                //props.VBW.SetValue(40e3 + 1e3 * i);

                //hostRepresentative.OnGUI_Value_Changed(props.VBW.HardwareProperty);

                //Task.Delay(timeout).Wait();

                Console.WriteLine( i.ToString() + " ====================================================");
            }

            Task.Delay(3000).Wait();
        }
#else
        [Test]
        public void SimpleHandMadeChain()
        {
            hostRepresentative = HostRepresentative.getInstance();
            state = State.getInstance();
            preprocessor = SettingsPreprocessor.getInstance();

            hostRepresentative.preprocessor = preprocessor;
            hostRepresentative.Init();

            state.UpdateInternalValues(false);
            state.ChangesHandled.WaitOne();
            preprocessor.SystemState_o = state;

            preprocessor.Init();
            preprocessor.Start();

            int timeout = 17;

            for (int i = 0; i < 3; i++)
            {

                hostRepresentative.OnGUI_Value_Changed(("CF", (200e6 + 1e6 * i).ToString()));

                Task.Delay(timeout).Wait();

                hostRepresentative.OnGUI_Value_Changed(("Span", (8e6 + 1e5 * i).ToString()));

                Task.Delay(timeout).Wait();

                //props.RBW.SetValue(90e3 + 1e3 * i);

                //hostRepresentative.OnGUI_Value_Changed(props.RBW.HardwareProperty);

                //Task.Delay(timeout).Wait();

                //props.VBW.SetValue(40e3 + 1e3 * i);

                //hostRepresentative.OnGUI_Value_Changed(props.VBW.HardwareProperty);

                //Task.Delay(timeout).Wait();

                Console.WriteLine( i.ToString() + " ====================================================");
            }

            Task.Delay(3000).Wait();
        }
#endif
    }
}
