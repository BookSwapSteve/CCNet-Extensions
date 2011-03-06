using Amazon;
using Amazon.SQS;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS
{
    /// <summary>
    /// Wrapper around static factory for AWS Clients to allow Constructor DI
    /// </summary>
    public class AwsFactory : IAwsFactoryWrapper
    {
        public AmazonSQS CreateAmazonSqsClient(string awsAccessKey,string awsSecretAccessKey)
        {
            return AWSClientFactory.CreateAmazonSQSClient(awsAccessKey, awsSecretAccessKey);
        }
    }
}
