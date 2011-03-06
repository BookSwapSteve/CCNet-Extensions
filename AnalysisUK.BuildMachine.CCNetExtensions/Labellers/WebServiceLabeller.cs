using System;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.Config;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Labellers
{
    /// <summary>
    /// Picks up the label to use from a webservice
    /// </summary>
    [ReflectorType("webServiceLabeller")]
    public class WebServiceLabeller : ILabeller, IConfigurationValidation
    {
        [ReflectorProperty("url", Required = true)]
        public virtual string Url { get; set; }

        public string Generate(IIntegrationResult integrationResult)
        {
            return GetLabelFromService();
        }

        public void Run(IIntegrationResult result)
        {
            result.Label = this.Generate(result);
        }

        public void Validate(IConfiguration configuration, ConfigurationTrace parent, IConfigurationErrorProcesser errorProcesser)
        {
            if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            {
                errorProcesser.ProcessError("Url is not valid");
            }
        }

        private string GetLabelFromService()
        {
            throw new NotImplementedException();
        }
    }
}
