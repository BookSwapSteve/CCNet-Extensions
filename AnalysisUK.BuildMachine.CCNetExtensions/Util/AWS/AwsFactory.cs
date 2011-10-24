using Amazon;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SimpleDB;

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

        public AmazonSimpleDB CreateAmazonSimpleDbClient(string awsAccessKey, string awsSecretAccessKey)
        {
            return AWSClientFactory.CreateAmazonSimpleDBClient(awsAccessKey, awsSecretAccessKey);
        }

        public AmazonS3 CreateAmazonS3Client(string awsAccessKey, string awsSecretAccessKey)
        {
            return AWSClientFactory.CreateAmazonS3Client(awsAccessKey, awsSecretAccessKey);
        }
    }
}
