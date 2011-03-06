using System;
using System.Collections.Generic;
using Amazon.SQS;
using Amazon.SQS.Model;
using AnalysisUK.BuildMachine.CCNetExtensions.Util.AWS;
using Exortech.NetReflector;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.Config;
using ThoughtWorks.CruiseControl.Core.Triggers;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Triggers
{
    /// <summary>
    /// Trigger on a message being present in Amazon Web Services Simple Queue Service.
    /// </summary>
    [ReflectorType("sqsTrigger")]
    public class SqsTrigger : ITrigger, IConfigurationValidation
    {
        private const int DefaultTriggerIntervalSeconds = 5;
        private readonly IAwsFactoryWrapper _awsFactoryWrapper;
        private AmazonSQS _client;

        #region Constructors

        public SqsTrigger()
            : this(new AwsFactory(), new IntervalTrigger { IntervalSeconds = DefaultTriggerIntervalSeconds })
        { }

        public SqsTrigger(IAwsFactoryWrapper awsFactoryWrapper, ITrigger innerTrigger)
        {
            _awsFactoryWrapper = awsFactoryWrapper;
            if (awsFactoryWrapper == null) throw new ArgumentNullException("awsFactoryWrapper");
            if (innerTrigger == null) throw new ArgumentNullException("innerTrigger");

            InnerTrigger = innerTrigger;

            // Default to force build
            BuildCondition = BuildCondition.ForceBuild;

            
        }

        #endregion

        #region CCNET visible properties

        [ReflectorProperty("awsAccessKey", Required = true)]
        public string AwsAccessKey { get; set; }

        [ReflectorProperty("awsSecretAccessKey", Required = true)]
        public string AwsSecretAccessKey { get; set; }

        [ReflectorProperty("queueUrl", Required = true)]
        public string QueueUrl { get; set; }

        [ReflectorProperty("buildCondition", Required = false)]
        public BuildCondition BuildCondition { get; set; }

        [ReflectorProperty("trigger", InstanceTypeKey = "type", Required = false)]
        public ITrigger InnerTrigger { get; set; }

        #endregion

        #region Public Properties

        public DateTime NextBuild
        {
            get { return DateTime.MaxValue; }
        }

        #endregion

        #region ITrigger Methods

        public void IntegrationCompleted()
        {
            InnerTrigger.IntegrationCompleted();
        }

        private void ConstructClientIfNeeded()
        {
            if (_client == null)
            {
                _client = _awsFactoryWrapper.CreateAmazonSqsClient(AwsAccessKey, AwsSecretAccessKey);
            }
        }

        public IntegrationRequest Fire()
        {
            ConstructClientIfNeeded();

            var request = new ReceiveMessageRequest { MaxNumberOfMessages = 10, QueueUrl = QueueUrl };

            ReceiveMessageResponse response = _client.ReceiveMessage(request);

            List<Amazon.SQS.Model.Message> messages = null;

            if (response.IsSetReceiveMessageResult())
            {
                messages = response.ReceiveMessageResult.Message;
            }

            if (messages != null && messages.Count > 0)
            {
                Log.Info("Found {0} SQS messages on queue", messages.Count);

                RemoveMessagesFromQueue(messages);

                return new IntegrationRequest(BuildCondition, GetType().Name, null);
            }
            return null;
        }

        /// <summary>
        /// Remove all the current messages from the Queue.
        /// </summary>
        /// <param name="messages"></param>
        /// <remarks>Queue message is only used to kick off a build so their is no significance to each message</remarks>
        private void RemoveMessagesFromQueue(IEnumerable<Amazon.SQS.Model.Message> messages)
        {
            ConstructClientIfNeeded();

            foreach (var message in messages)
            {
                var request = new DeleteMessageRequest { QueueUrl = QueueUrl, ReceiptHandle = message.ReceiptHandle };

                _client.DeleteMessage(request);
            }
        }

        #endregion

        #region IConfigurationValidation Methods

        public void Validate(IConfiguration configuration, ConfigurationTrace parent, IConfigurationErrorProcesser errorProcesser)
        {
            if (string.IsNullOrEmpty(AwsAccessKey))
            {
                errorProcesser.ProcessError("AwsAccessKey cannot be empty");
            }

            if (string.IsNullOrEmpty(AwsSecretAccessKey))
            {
                errorProcesser.ProcessError("AwsSecretAccessKey cannot be empty");
            }

            if (!Uri.IsWellFormedUriString(QueueUrl, UriKind.Absolute))
            {
                errorProcesser.ProcessError("QueueUrl is not wellformed");
            }
        }

        #endregion
    }
}
