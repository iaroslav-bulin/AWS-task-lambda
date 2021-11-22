using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SQS;
using Amazon.SQS.Model;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLoggerLambda
{
    public class Function
    {
        private readonly string _bucketName = "stack-logstorage";

        private readonly string _queueName = "stack-sqsloggerqueue";

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        /// 
        public Function()
        {
        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
                using (var s3client = new AmazonS3Client(Amazon.RegionEndpoint.USEast2))
                {
                var fileName = DateTime.Now.Date.ToString("yyyy-MM-dd");

                var logData = message.Body;

                var buckets = await s3client.ListBucketsAsync();
                var bucketExist = buckets.Buckets.Any(b => b.BucketName == _bucketName);
                if (!bucketExist)
                {
                    throw new AmazonS3Exception($"bucket - {_bucketName} doesnt exist");
                }

                AmazonSQSClient sqsClient = new AmazonSQSClient();
                var queueUrlResp = await sqsClient.GetQueueUrlAsync(_queueName);
                context.Logger.LogLine($"queueUrlResp.QueueUrl = {queueUrlResp.QueueUrl}");
                context.Logger.LogLine($"message.ReceiptHandle = {message.ReceiptHandle}");
                try
                {
                    var deleteMsgResp = await sqsClient.DeleteMessageAsync(queueUrlResp.QueueUrl, message.ReceiptHandle);

                    if (deleteMsgResp.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    {
                        context.Logger.LogLine($"Failed to delete message {message.ReceiptHandle} from queue {_queueName}");
                    }
                }
                catch (Exception e)
                {
                    context.Logger.LogLine(e.Message);
                }

                using (var newMemoryStream = new MemoryStream())
                    {
                        var logDataArr = Encoding.Unicode.GetBytes(logData);
                        await newMemoryStream.WriteAsync(logDataArr, 0, logDataArr.Length);

                        var req = new PutObjectRequest()
                        {
                            InputStream = newMemoryStream,
                            BucketName = _bucketName,
                            Key = fileName
                        };
                        await s3client.PutObjectAsync(req);
                    }
                    context.Logger.LogLine($"(message.Body) -> {message.Body}");
                }
        }
    }

}
