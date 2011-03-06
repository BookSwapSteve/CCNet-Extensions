using System;
using System.IO;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.State;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace AnalysisUK.BuildMachine.CCNetExtensions.State
{
    [ReflectorType("webServiceState")]
    public class WebServiceState : IStateManager
    {
        public WebServiceState()
        {
        }

        ////public WebServiceStateManager(IFileSystem fileSystem)
        ////{
        ////}

        public IIntegrationResult LoadState(string project)
        {
            throw new NotImplementedException("");
        }

        public void SaveState(IIntegrationResult result)
        {
            throw new NotImplementedException("");
        }

        public bool HasPreviousState(string project)
        {
            throw new NotImplementedException("");
        }
    }
}
