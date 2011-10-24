using Amazon.S3;
using Amazon.SQS;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS
{
    public interface IAwsFactoryWrapper
    {
        AmazonSQS CreateAmazonSqsClient(string awsAccessKey, string awsSecretAccessKey);
        Amazon.SimpleDB.AmazonSimpleDB CreateAmazonSimpleDbClient(string awsAccessKey, string awsSecretAccessKey);
        AmazonS3 CreateAmazonS3Client(string awsAccessKey, string awsSecretAccessKey);
    }
}
