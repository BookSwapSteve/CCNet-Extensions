using Amazon.SQS;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS
{
    public interface IAwsFactoryWrapper
    {
        AmazonSQS CreateAmazonSqsClient(string awsAccessKey, string awsSecretAccessKey);
    }
}
