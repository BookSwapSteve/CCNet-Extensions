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
using Message = Amazon.SQS.Model.Message;

namespace AnalysisUK.BuildMachine.CCNetExtensions.Triggers
{
    /// <summary>
    /// Trigger integration on a message being present in Amazon Web Services Simple Queue Service.
    /// </summary>
    /// <seealso cref="http://aws.amazon.com/sqs/"/>
    [ReflectorType("sqsTrigger")]
    public class SqsTrigger : ITrigger, IConfigurationValidation
    {
        #region Private fields

        private const double DefaultTriggerIntervalSeconds = 30;
        private readonly IAwsFactoryWrapper _awsFactoryWrapper;
        private AmazonSQS _client;
        private string _name;
        private double _intervalSeconds = DefaultTriggerIntervalSeconds;
        private readonly List<Message> _messages = new List<Message>();

        #endregion

        #region Constructors

        public SqsTrigger()
            : this(new DateTimeProvider(),
                    new AwsFactory(),
                    new IntervalTrigger { IntervalSeconds = DefaultTriggerIntervalSeconds })
        { }

        public SqsTrigger(DateTimeProvider dateTimeProvider, IAwsFactoryWrapper awsFactoryWrapper, ITrigger innerTrigger)
        {
            if (dateTimeProvider == null) throw new ArgumentNullException("dateTimeProvider");
            if (awsFactoryWrapper == null) throw new ArgumentNullException("awsFactoryWrapper");
            if (innerTrigger == null) throw new ArgumentNullException("innerTrigger");

            _awsFactoryWrapper = awsFactoryWrapper;
            InnerTrigger = innerTrigger;

            // Default to force build
            BuildCondition = BuildCondition.ForceBuild;
        }

        #endregion

        #region CCNET visible properties

        /// <summary>
        /// The name of the trigger. This name is passed to external tools as a means to identify the trigger that requested the build.
        /// </summary>
        /// <version>1.1</version>
        /// <default>IntervalTrigger</default>
        [ReflectorProperty("name", Required = false)]
        public virtual string Name
        {
            get
            {
                if (_name == null)
                {
                    _name = GetType().Name;
                }
                return _name;
            }
            set { _name = value; }
        }

        /// <summary>
        /// The number of seconds after an integration cycle completes before triggering the next integration cycle.
        /// </summary>
        /// <default>30</default>
        /// <remarks>Uses IntervalTrigger inner timer, not valid if a different InnerTrigger is used.</remarks>
        [ReflectorProperty("seconds", Required = false)]
        public double IntervalSeconds
        {
            get { return _intervalSeconds; }
            set
            {
                _intervalSeconds = value;
                SetIntervalTrigger();
            }
        }

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
        /// Queue Url to watch.
        /// </summary>
        /// <value>The Url of the SQS queue to watch for messages to trigger a build. The queue should be unique for the project and not shared.</value>
        [ReflectorProperty("queueUrl", Required = true)]
        public string QueueUrl { get; set; }

        /// <summary>
        /// Build condition to set when a message is found on the queue
        /// </summary>
        [ReflectorProperty("buildCondition", Required = false)]
        public BuildCondition BuildCondition { get; set; }

        /// <summary>
        /// InnerTrigger used to trigger message queue check
        /// </summary>
        public ITrigger InnerTrigger { get; set; }

        #endregion

        #region ITrigger Methods

        /// <summary>
        /// Returns the time of the next build.
        /// </summary>
        /// <value></value>
        public DateTime NextBuild
        {
            get { return InnerTrigger.NextBuild; }
        }

        /// <summary>
        /// Notifies the trigger that an integration has completed.
        /// </summary>
        public void IntegrationCompleted()
        {
            InnerTrigger.IntegrationCompleted();

            // Now the integration is complete remove the messages that triggered the integration
            // so ccnet dies or is killed before completion it will get forced again
            // by the message being on the queue.
            RemoveMessagesFromQueue();
        }

        /// <summary>
        /// Fires this instance.
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public IntegrationRequest Fire()
        {
            if (InnerTrigger.Fire() == null)
            {
                return null;
            }

            try
            {
                Log.Info("sqsTrigger checking for messages on queue {0}", QueueUrl);

                if (ShouldRunIntegration() == BuildCondition.NoBuild)
                {
                    return null;
                }
                return new IntegrationRequest(BuildCondition, GetType().Name, null);
            }
            finally
            {
                // Reset the inner trigger
                InnerTrigger.IntegrationCompleted();
            }
        }

        #endregion

        #region IConfigurationValidation Methods

        /// <summary>
        /// Checks the internal validation of the item.
        /// </summary>
        /// <param name="configuration">The entire configuration.</param>
        /// <param name="parent">The parent item for the item being validated.</param>
        /// <param name="errorProcesser">The error processer to use.</param>
        public void Validate(IConfiguration configuration, ConfigurationTrace parent, IConfigurationErrorProcesser errorProcesser)
        {
            if (string.IsNullOrEmpty(AwsAccessKey))
            {
                errorProcesser.ProcessError("awsAccessKey cannot be empty");
            }

            if (string.IsNullOrEmpty(AwsSecretAccessKey))
            {
                errorProcesser.ProcessError("awsSecretAccessKey cannot be empty");
            }

            if (!Uri.IsWellFormedUriString(QueueUrl, UriKind.Absolute))
            {
                errorProcesser.ProcessError("queueUrl is not well-formed");
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Set the interval of the inner trigger
        /// </summary>
        private void SetIntervalTrigger()
        {
            if (InnerTrigger is IntervalTrigger)
            {
                ((IntervalTrigger)InnerTrigger).IntervalSeconds = _intervalSeconds;
            }
        }

        /// <summary>
        /// Determine if integration should run.
        /// </summary>
        /// <returns></returns>
        private BuildCondition ShouldRunIntegration()
        {
            ConstructClientIfNeeded();

            var queuedMessages = GetMessagesFromQueue();

            if (queuedMessages.Count > 0)
            {
                Log.Info("Found {0} SQS messages on queue {1}", queuedMessages.Count, QueueUrl);

                _messages.AddRange(queuedMessages);

                return BuildCondition;
            }
            return BuildCondition.NoBuild;
        }

        /// <summary>
        /// Construct a AWS SQS client if needed
        /// </summary>
        private void ConstructClientIfNeeded()
        {
            if (_client == null)
            {
                _client = _awsFactoryWrapper.CreateAmazonSqsClient(AwsAccessKey, AwsSecretAccessKey);
            }
        }

        /// <summary>
        /// Get the messages from the SQS Queue
        /// </summary>
        /// <returns></returns>
        private List<Message> GetMessagesFromQueue()
        {
            try
            {
                var request = new ReceiveMessageRequest { MaxNumberOfMessages = 10, QueueUrl = QueueUrl };

                ReceiveMessageResponse response = _client.ReceiveMessage(request);

                if (response.IsSetReceiveMessageResult())
                {
                    return response.ReceiveMessageResult.Message;
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Failed getting messages from queue {0}. Exception: ", QueueUrl, ex);
                return null;
            }
        }

        /// <summary>
        /// Remove all the current messages from the Queue.
        /// </summary>
        /// <remarks>Queue message is only used to kick off a build so their is no significance to each message</remarks>
        private void RemoveMessagesFromQueue()
        {
            Log.Info("Trying to remove {0} messages from SQS Queue {1}", _messages.Count, QueueUrl);

            if (_messages.Count == 0)
            {
                Log.Warning("Trying to remove messages from SQS Queue {0} but no messages listed as need removing. Build not triggered by sqsTrigger?", QueueUrl);
            }

            ConstructClientIfNeeded();

            RemoveMessages();
        }

        private void RemoveMessages()
        {
            foreach (var message in _messages)
            {
                RemoveMessage(message);
            }

            _messages.Clear();
        }

        private void RemoveMessage(Message message)
        {
            try
            {
                Log.Info("Deleting SQS message {0} on queue: {1}", message.MessageId, QueueUrl);
                var request = new DeleteMessageRequest {QueueUrl = QueueUrl, ReceiptHandle = message.ReceiptHandle};

                _client.DeleteMessage(request);
            }
            catch (Exception ex)
            {
                Log.Error("Error removing message:{0}", message.ReceiptHandle);
                Log.Error(ex);
            }
        }

        #endregion
    }
}
