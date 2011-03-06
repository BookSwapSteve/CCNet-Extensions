using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.State;

namespace AnalysisUK.BuildMachine.CCNetExtensions.State
{
    /// <summary>
    /// Use Amazon's AWS SimpleDB storage for state
    /// </summary>
    /// <remarks>
    /// This allows build machines to store state off machine and allows the build to be moved
    /// between machines without having to have the state moved with it.
    /// 
    /// This should be useful when using EC2 machines.</remarks>
    [ReflectorType("simpleDBState")]
    public class SimpleDBState : IStateManager
    {
        public SimpleDBState()
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
