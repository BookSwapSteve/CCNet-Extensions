using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.State;

namespace AnalysisUK.BuildMachine.CCNetExtensions.State
{
    /// <summary>
    /// Use Amazon's AWS S3 storage for state.
    /// </summary>
    /// <remarks>
    /// This allows build machines to store state off machine and allows the build to be moved
    /// between machines without having to have the state moved with it.
    /// 
    /// This should be useful when using EC2 machines.</remarks>
    [ReflectorType("s3State")]
    public class S3StateManager : IStateManager
    {
        private readonly IAwsFactoryWrapper _awsFactoryWrapper;
        private readonly IStateManager _fallbackStateManager;
        private bool _bucketCreated = false;

        public S3StateManager()
            : this(new AwsFactory(), new FileStateManager())
        { }

        public S3StateManager(IAwsFactoryWrapper awsFactoryWrapper, IStateManager fallbackStateManager)
        {
            if (awsFactoryWrapper == null) throw new ArgumentNullException("awsFactoryWrapper");
            if (fallbackStateManager == null) throw new ArgumentNullException("fallbackStateManager");

            _awsFactoryWrapper = awsFactoryWrapper;
            _fallbackStateManager = fallbackStateManager;
            BucketRegion = S3Region.US;
        }

        public IIntegrationResult LoadState(string project)
        {
            if (DoesStateFileExist(project))
            {
                return LoadStateFromS3(project);
            }

            if (FallbackToFileState)
            {
                // If no result was found on S3 then try and load the local file
                return _fallbackStateManager.LoadState(project);
            }

            throw new CruiseControlException("No state found for project " + project);
        }

        public void SaveState(IIntegrationResult result)
        {
            try
            {
                SaveStateToS3(result);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Failed to save state to AWS S3", ex);
            }
            finally
            {
                // Also save the state
                if (FallbackToFileState)
                {
                    _fallbackStateManager.SaveState(result);
                }
            }
        }

        public bool HasPreviousState(string project)
        {
            bool hasState = DoesStateFileExist(project);

            if (hasState)
            {
                return true;
            }

            if (FallbackToFileState)
            {
                return _fallbackStateManager.HasPreviousState(project);
            }

            return false;
        }

        #region Public Properties

        /// <summary>
        /// AWS Access Key for user access to SQS Queue
        /// </summary>
        /// <remarks>The AWS user needs permission to read the SQS specified.</remarks>
        [ReflectorProperty("awsAccessKey", Required = true)]
        public string AwsAccessKey { get; set; }

        /// <summary>
        /// AWS Secret Access Key for user access to SQS Queue
        /// </summary>
        [ReflectorProperty("awsSecretAccessKey", Required = true)]
        public string AwsSecretAccessKey { get; set; }

        /// <summary>
        /// AWS S3 Bucket name. Will be created if it does not exist.
        /// </summary>
        [ReflectorProperty("bucket", Required = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// The region to create the bucket in.
        /// </summary>
        /// <remarks>Values are ASP1, EU, SFO, US</remarks>
        [ReflectorProperty("bucketRegion ", Required = false)]
        public S3Region BucketRegion { get; set; }

        /// <summary>
        /// Allow the state manager to fall back to local file state when reading 
        /// state if it is not stored on S3.
        /// </summary>
        [ReflectorProperty("fallbackToFileState", Required = false)]
        public bool FallbackToFileState { get; set; }

        #endregion

        private void SaveStateToS3(IIntegrationResult result)
        {
            string state = SerializeState(result);

            var request = new PutObjectRequest
                              {
                                  ContentType = "text/xml",
                                  ContentBody = state,
                                  BucketName = Bucket,
                                  Key = result.ProjectName + ".xml",
                              };

            CreateBucket();
            CreateAmazonS3Client().PutObject(request);
        }

        private string SerializeState(IIntegrationResult result)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new XmlSerializer(typeof(IntegrationResult));
                serializer.Serialize(stream, result);

                stream.Position = 0;

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private IIntegrationResult LoadStateFromS3(string projectName)
        {
            try
            {
                var request = new GetObjectRequest
                                  {
                                      BucketName = Bucket,
                                      Key = projectName + ".xml"
                                  };

                var response = CreateAmazonS3Client().GetObject(request);

                if (response.ContentLength > 0 && response.ResponseStream != null)
                {

                    var serializer = new XmlSerializer(typeof(IntegrationResult));
                    return serializer.Deserialize(response.ResponseStream) as IIntegrationResult;
                }
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Error loading state from S3", ex);
            }
            throw new CruiseControlException("Failed to load state from S3");
        }

        private bool DoesStateFileExist(string projectName)
        {
            try
            {
                var request = new ListObjectsRequest { Prefix = projectName + ".xml", BucketName = Bucket };

                CreateBucket();
                ListObjectsResponse response = CreateAmazonS3Client().ListObjects(request);

                return response.S3Objects.Count > 0;

            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Error loading state from S3", ex);
            }
            throw new CruiseControlException("Failed to load state from S3");
        }

        /// <summary>
        /// Creates the bucket. If the bucket already exists no action is taken.
        /// </summary>
        private void CreateBucket()
        {
            if (_bucketCreated)
            {
                return;
            }

            try
            {
                var request = new PutBucketRequest { BucketName = Bucket, BucketRegion = BucketRegion };
                CreateAmazonS3Client().PutBucket(request);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Failed to create bucket " + Bucket, ex);
            }
        }

        private AmazonS3 CreateAmazonS3Client()
        {
            return _awsFactoryWrapper.CreateAmazonS3Client(AwsAccessKey, AwsSecretAccessKey);
        }
    }
}