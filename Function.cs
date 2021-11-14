using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Transfer;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLoggerLambda
{
    public class Function
    {
        private readonly string _bucketName = "loggerstorage";
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
            //string jsonString = JsonSerializer.Serialize(evnt);
            //Console.WriteLine(jsonString);

            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            //Console.WriteLine(message.Body);
            //await Task.CompletedTask;

            using (var s3client = new AmazonS3Client(Amazon.RegionEndpoint.USEast2))
            {
                var fileName = DateTime.Now.Date.ToString("yyyy-MM-dd");
                var logData = message.Body;

                using (var newMemoryStream = new MemoryStream())
                {
                    var logDataArr = Encoding.Unicode.GetBytes(logData);
                    await newMemoryStream.WriteAsync(logDataArr, 0, logDataArr.Length);

                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = newMemoryStream,
                        Key = fileName,
                        BucketName = _bucketName,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    var fileTransferUtility = new TransferUtility(s3client);
                    await fileTransferUtility.UploadAsync(uploadRequest);
                }


                context.Logger.LogLine($"Processed message {message.Body}");
            }
        }
    }

}
