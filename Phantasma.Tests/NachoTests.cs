using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.API;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.Storage;
using Phantasma.VM;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    /*
    [TestClass]
    class NachoTests
    {
        [TestMethod]
        public void TestGetBotWrestler()
        {
            var owner       = PhantasmaKeys.Generate();
            var simulator   = new NexusSimulator(owner, 1234);
            var nexus       = simulator.Nexus;
            var api         = new NexusAPI(nexus);


            var callScript      = ScriptUtils.BeginScript().CallContract("nacho", "GetWrestler", new object[] {-1}).EndScript();
            var apiResult       = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(callScript));
            var bytes           = Base16.Decode(apiResult.results[0]);
            var objResult       = Serialization.Unserialize<VMObject>(bytes);
            var nachoWrestler   = objResult.ToStruct<NachoWrestler>();

        }

    }*/
}
