using Amazon;
using Amazon.CostAndUsageReport;
using Amazon.CostAndUsageReport.Model;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using BAMCIS.AWSLambda.Common.CustomResources;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Xunit;

namespace AWSCostAndUsageReport.Tests
{
    public class FunctionTest
    {
        #region Private Static Fields

        private static string AccountNumber = "123456789012";
        private static string Region = "us-east-1";
        private static string OutputBucket = $"{Environment.UserName}-billing";
        private static string PresignedUrlBucket = $"{Environment.UserName}-presigned-url-test";
        private static IAmazonS3 S3Client = new AmazonS3Client();

        #endregion

        #region Constructors

        public FunctionTest()
        {
        }

        #endregion

        #region Unit Tests

        [Fact]
        public async Task RunTests()
        {
            AWSConfigs.AWSProfilesLocation = $"{Environment.GetEnvironmentVariable("UserProfile")}\\.aws\\credentials";

            await TestCURCreate("TestReport");

            await TestCURUpdate("TestReport", "TestReport2");

            await TestCURDelete("TestReport2");
        }

        #endregion

        #region Private Functions

        private static string GenerateCreateJson(string reportName)
        {
            PutReportDefinitionRequest ReportRequest = new PutReportDefinitionRequest()
            {
                ReportDefinition = new ReportDefinition()
                {
                    Compression = CompressionFormat.GZIP,
                    Format = ReportFormat.TextORcsv,
                    S3Bucket = OutputBucket,
                    S3Prefix = $"{AccountNumber}/",
                    S3Region = Region,
                    TimeUnit = TimeUnit.DAILY
                }
            };

        GetPreSignedUrlRequest Req = new GetPreSignedUrlRequest()
            {
                BucketName = PresignedUrlBucket,
                Key = "result.txt",
                Expires = DateTime.Now.AddMinutes(2),
                Protocol = Protocol.HTTPS,
                Verb = HttpVerb.PUT
            };

            string PreSignedUrl = S3Client.GetPreSignedURL(Req);
            string Json = $@"
{{
""requestType"":""create"",
""responseUrl"":""{PreSignedUrl}"",
""stackId"":""arn:aws:cloudformation:{Region}:{AccountNumber}:stack/stack-name/{Guid.NewGuid().ToString()}"",
""requestId"":""12345678"",
""resourceType"":""Custom::TestResource"",
""logicalResourceId"":""{reportName}"",
""resourceProperties"":{JsonConvert.SerializeObject(ReportRequest)}
}}";

            Json = Json.Trim().Replace("\r", "").Replace("\n", "").Replace("\t", "");

            return Json;
        }

        private static string GenerateUpdateJson(string oldReportName, string newReportName)
        {
            PutReportDefinitionRequest OldReportRequest = new PutReportDefinitionRequest()
            {
                ReportDefinition = new ReportDefinition()
                {
                    Compression = CompressionFormat.GZIP,
                    Format = ReportFormat.TextORcsv,
                    S3Bucket = OutputBucket,
                    S3Prefix = $"{AccountNumber}/",
                    S3Region = Region,
                    TimeUnit = TimeUnit.DAILY,
                    ReportName = oldReportName
                }
            };

            PutReportDefinitionRequest NewReportRequest = new PutReportDefinitionRequest()
            {
                ReportDefinition = new ReportDefinition()
                {
                    Compression = CompressionFormat.GZIP,
                    Format = ReportFormat.TextORcsv,
                    S3Bucket = OutputBucket,
                    S3Prefix = $"{AccountNumber}/",
                    S3Region = Region,
                    TimeUnit = TimeUnit.DAILY,
                    ReportName = newReportName
                }
            };

            GetPreSignedUrlRequest Req = new GetPreSignedUrlRequest()
            {
                BucketName = PresignedUrlBucket,
                Key = "result.txt",
                Expires = DateTime.Now.AddMinutes(2),
                Protocol = Protocol.HTTPS,
                Verb = HttpVerb.PUT
            };

            string PreSignedUrl = S3Client.GetPreSignedURL(Req);
            string Json = $@"
{{
""requestType"":""update"",
""responseUrl"":""{PreSignedUrl}"",
""stackId"":""arn:aws:cloudformation:{Region}:{AccountNumber}:stack/stack-name/{Guid.NewGuid().ToString()}"",
""requestId"":""12345678"",
""resourceType"":""Custom::TestResource"",
""logicalResourceId"":""{oldReportName}"",
""physicalResourceId"":""{oldReportName}"",
""resourceProperties"":{JsonConvert.SerializeObject(NewReportRequest)},
""oldResourceProperties"":{JsonConvert.SerializeObject(OldReportRequest)}
}}";

            Json = Json.Trim().Replace("\r", "").Replace("\n", "").Replace("\t", "");

            return Json;
        }

        private static string GenerateDeleteJson(string reportName)
        {
            PutReportDefinitionRequest ReportRequest = new PutReportDefinitionRequest()
            {
                ReportDefinition = new ReportDefinition()
                {
                    Compression = CompressionFormat.GZIP,
                    Format = ReportFormat.TextORcsv,
                    S3Bucket = OutputBucket,
                    S3Prefix = $"{AccountNumber}/",
                    S3Region = Region,
                    TimeUnit = TimeUnit.DAILY
                }
            };

            GetPreSignedUrlRequest Req = new GetPreSignedUrlRequest()
            {
                BucketName = PresignedUrlBucket,
                Key = "result.txt",
                Expires = DateTime.Now.AddMinutes(2),
                Protocol = Protocol.HTTPS,
                Verb = HttpVerb.PUT
            };

            string PreSignedUrl = S3Client.GetPreSignedURL(Req);
            string Json = $@"
{{
""requestType"":""delete"",
""responseUrl"":""{PreSignedUrl}"",
""stackId"":""arn:aws:cloudformation:{Region}:{AccountNumber}:stack/stack-name/{Guid.NewGuid().ToString()}"",
""requestId"":""12345678"",
""resourceType"":""Custom::TestResource"",
""logicalResourceId"":""{reportName}"",
""physicalResourceId"":""{reportName}"",
""resourceProperties"":{JsonConvert.SerializeObject(ReportRequest)}
}}";

            Json = Json.Trim().Replace("\r", "").Replace("\n", "").Replace("\t", "");

            return Json;
        }

        private async Task TestCURCreate(string reportName)
        {
            string Json = GenerateCreateJson(reportName);

            CustomResourceRequest Request = JsonConvert.DeserializeObject<CustomResourceRequest>(Json);

            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "CostAndUsageReportResource",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            // ACT
            Entrypoint Ep = new Entrypoint();
            await Ep.Execute(Request, Context);
        }

        private async Task TestCURUpdate(string oldReportName, string newReportName)
        {
            // ARRANGE          
            string Json = GenerateUpdateJson(oldReportName, newReportName);
                                
            CustomResourceRequest Request = JsonConvert.DeserializeObject<CustomResourceRequest>(Json);

            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "CostAndUsageReportResource",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            // ACT
            Entrypoint Ep = new Entrypoint();
            await Ep.Execute(Request, Context);
        }

        private async Task TestCURDelete(string reportName)
        {
            // ARRANGE          
            string Json = GenerateDeleteJson(reportName);

            CustomResourceRequest Request = JsonConvert.DeserializeObject<CustomResourceRequest>(Json);

            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            SharedCredentialsFile Creds = new SharedCredentialsFile();
            Creds.TryGetProfile($"{Environment.UserName}-dev", out CredentialProfile Profile);

            ImmutableCredentials Cr = Profile.GetAWSCredentials(Creds).GetCredentials();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "CostAndUsageReportResource",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            // ACT
            Entrypoint Ep = new Entrypoint();
            await Ep.Execute(Request, Context);

            // ASSERT
        }

        #endregion
    }
}
