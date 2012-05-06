using System;
using System.IO;
using Amazon.S3;
using Amazon.S3.Model;
using AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.Tasks;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Publishers
{
    public class S3BuildPublisher : TaskBase, IAwsTask, IDisposable
    {
        private readonly IAwsFactoryWrapper _awsFactoryWrapper;
        private AmazonS3 _client;

        #region Constructors

        public S3BuildPublisher()
            : this(new AwsFactory())
        { }

        public S3BuildPublisher(IAwsFactoryWrapper awsFactoryWrapper)
        {
            _awsFactoryWrapper = awsFactoryWrapper;
            if (awsFactoryWrapper == null) throw new ArgumentNullException("awsFactoryWrapper");
            _client = _awsFactoryWrapper.CreateAmazonS3Client(AwsAccessKey, AwsSecretAccessKey);

            BucketRegion = S3Region.US;
        }

        #endregion

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
        /// The S3 bucket to copy the files to. This path can be absolute or can be relative to the project's
        /// artifact directory. If <b>useLabelSubDirectory</b> is true (default) a subdirectory with the
        /// current build's label will be created, and the contents of sourceDir will be copied to it. If
        /// unspecified, the project's artifact directory will be used as the publish directory.
        /// </summary>
        /// <default>n/a</default>
        [ReflectorProperty("bucket", Required = true)]
        public string Bucket { get; set; }

        /// <summary>
        /// The region to create the bucket in if it doesn't already exist.
        /// </summary>
        /// <remarks>Values are ASP1, EU, SFO, US</remarks>
        /// <default>US</default>
        [ReflectorProperty("bucketRegion ", Required = false)]
        public S3Region BucketRegion { get; set; }

        /// <summary>
        /// The source directory to copy files from. This path can be absolute or can be relative to the
        /// project's working directory. If unspecified, the project's working directory will be used as the
        /// source directory.
        /// </summary>
        /// <default>n/a</default>
        [ReflectorProperty("sourceDir", Required = false)]
        public string SourceDir { get; set; }

        /// <summary>
        /// If set to true (the default value), files will be copied to subdirectory under the publishDir which
        /// will be named with the label for the current integration.
        /// </summary>
        /// <default>true</default>
        [ReflectorProperty("useLabelSubDirectory", Required = false)]
        public bool UseLabelSubDirectory { get; set; }

        /// <summary>
        /// Always copies the files, regardless of the state of the build.
        /// </summary>
        /// <default>false</default>
        [ReflectorProperty("alwaysPublish", Required = false)]
        public bool AlwaysPublish { get; set; }

        /// <summary>
        /// Cleans the publishDir if it exists, so that you will always have an exact copy of the sourceDir.
        /// </summary>
        /// <version>1.5</version>
        /// <default>false</default>
        [ReflectorProperty("emptyBucketPriorToCopy", Required = false)]
        public bool EmptyBucketPriorToCopy { get; set; }

        /// <summary>
        /// Try to create the bucket on execute.
        /// </summary>
        /// <version>1.5</version>
        /// <default>false</default>
        /// <remarks>Set to false if the bucket already exists otherwise if many projects try to create the bucket aws 
        /// may will throw a resource being modified exception causing problems on CCNET start.</remarks>
        [ReflectorProperty("createBucket", Required = false)]
        public bool CreateBucket { get; set; }

        #endregion

        #region Task Implementation

        protected override bool Execute(IIntegrationResult result)
        {
            result.BuildProgressInformation.SignalStartRunTask(!string.IsNullOrEmpty(Description) ? Description : "Publishing build results to S3 bucket " + Bucket);

            if (result.Succeeded || AlwaysPublish)
            {
                string publishFolder = "/";
                var sourceDirectoryInfo = new DirectoryInfo(result.BaseFromWorkingDirectory(SourceDir));

                if (EmptyBucketPriorToCopy)
                {
                    DeleteBucket();
                    EnsureBucketExists();
                }

                if (CreateBucket)
                {
                    EnsureBucketExists();
                }

                if (UseLabelSubDirectory)
                {
                    publishFolder = string.Format(@"{0}/", result.Label);
                    CreateFolder(publishFolder);
                }

                RecurseSubDirectories(sourceDirectoryInfo, publishFolder);

                // TODO: Implement cleanup.
            }

            return true;
        }

        /// <summary>
        /// Ensure that the bucket exists. 
        /// </summary>
        /// <remarks>
        /// This creates the bucket assuming AWS will ignore the fact one already exists if
        /// it does already exist.</remarks>
        private void EnsureBucketExists()
        {
            try
            {
                var request = new PutBucketRequest { BucketName = Bucket, BucketRegion = BucketRegion };
                _client.PutBucket(request);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Failed to create bucket " + Bucket, ex);
            }
        }


        private void DeleteBucket()
        {
            try
            {
                var request = new DeleteBucketRequest().WithBucketName(Bucket);
                _client.DeleteBucket(request);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Failed to delete bucket " + Bucket, ex);
            }
        }

        /// <summary>
        /// Create folder in S3
        /// </summary>
        /// <param name="publishFolder"></param>
        private void CreateFolder(string publishFolder)
        {
            try
            {
                var request = new PutObjectRequest().WithBucketName(Bucket).WithKey(publishFolder);

                _client.PutObject(request);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException("Failed to create folder" + publishFolder, ex);
            }
        }

        /// <summary>
        /// Copies all files and folders from sourceDirectory to publishFolder
        /// </summary>
        /// <param name="sourceDirectory">The source directory to copy from.</param>
        /// <param name="publishFolder">Folder in AWS Bucket to publish to.</param>
        private void RecurseSubDirectories(DirectoryInfo sourceDirectory, string publishFolder)
        {
            FileInfo[] files = sourceDirectory.GetFiles();
            foreach (FileInfo file in files)
            {
                string destFile = Path.Combine(publishFolder, file.Name);

                CopyFileToS3(file, destFile);
            }

            DirectoryInfo[] subDirectories = sourceDirectory.GetDirectories();
            foreach (DirectoryInfo subDir in subDirectories)
            {
                string folder = Path.Combine(publishFolder, subDir.Name);

                RecurseSubDirectories(subDir, folder);
            }
        }

        private void CopyFileToS3(FileInfo file, string destFile)
        {
            try
            {
                var request = new PutObjectRequest()
                                  {
                                      BucketName = Bucket,
                                      Key = destFile,
                                      FilePath = file.FullName
                                  };

                _client.PutObject(request);
            }
            catch (Exception ex)
            {
                throw new CruiseControlException(string.Format("Failed to copy file {0} to S3 {1}", file.FullName, destFile) , ex);
            }
        }

        #endregion

        #region IDispoable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        virtual protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }    
            }
        }

        #endregion
    }
}
