using Exortech.NetReflector;

namespace AnalysisUK.BuildMachine.CCNetExtensions
{
    public interface IAwsTask
    {
        /// <summary>
        /// AWS Access Key for user.
        /// </summary>
        [ReflectorProperty("awsAccessKey", Required = true)]
        string AwsAccessKey { get; set; }

        /// <summary>
        /// AWS Secret Access Key for user.
        /// </summary>
        [ReflectorProperty("awsSecretAccessKey", Required = true)]
        string AwsSecretAccessKey { get; set; }
    }
}
