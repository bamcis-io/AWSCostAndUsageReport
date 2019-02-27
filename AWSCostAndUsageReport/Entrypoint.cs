using Amazon.CostAndUsageReport;
using Amazon.CostAndUsageReport.Model;
using Amazon.Lambda.Core;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSLambda.Common.CustomResources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWSCostAndUsageReport
{
    /// <summary>
    /// The entrypoint for the lambda function
    /// </summary>
    public class Entrypoint
    {
        #region Private Fields

        /// <summary>
        /// The client handler for creating custom resources
        /// </summary>
        private ICustomResourceHandler _Handler;

        /// <summary>
        /// The default report format
        /// </summary>
        private ReportFormat DefaultFormat = ReportFormat.TextORcsv;

        /// <summary>
        /// The default compression for the report files
        /// </summary>
        private CompressionFormat DefaultCompression = CompressionFormat.GZIP;

        /// <summary>
        /// The default time unit for the reports.
        /// </summary>
        private TimeUnit DefaultTimeUnit = TimeUnit.DAILY;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Entrypoint()
        {
            Func<CustomResourceRequest, ILambdaContext, Task<CustomResourceResponse>> CreateAsync = async (request, context) =>
            {
                string ReportName = String.Empty;

                try
                {
                    //arn:aws:lambda:us-east-1:123456789012:function:FunctionName
                    string[] Parts = context.InvokedFunctionArn.Split(":");
                    string Region = Parts[3];
                    string AccountId = Parts[4];

                    AmazonCostAndUsageReportConfig Config = new AmazonCostAndUsageReportConfig();
                    IAmazonCostAndUsageReport Client = new AmazonCostAndUsageReportClient(Config);

                    PutReportDefinitionRequest Request = JsonConvert.DeserializeObject<PutReportDefinitionRequest>(JsonConvert.SerializeObject(request.ResourceProperties));

                    if (String.IsNullOrEmpty(Request.ReportDefinition.ReportName))
                    {
                        Request.ReportDefinition.ReportName = request.LogicalResourceId;
                    }

                    ReportName = Request.ReportDefinition.ReportName;

                    if (Request.ReportDefinition.AdditionalSchemaElements == null)
                    {
                        Request.ReportDefinition.AdditionalSchemaElements = new List<string>();
                    }

                    // This is required to prevent this error: Value null at 'reportDefinition.additionalSchemaElements' failed to satisfy constraint: Member must not be null
                    if (!Request.ReportDefinition.AdditionalSchemaElements.Contains("RESOURCES"))
                    {
                        Request.ReportDefinition.AdditionalSchemaElements.Add("RESOURCES");
                    }

                    // Setup defaults for the definition
                    if (String.IsNullOrEmpty(Request.ReportDefinition.S3Region))
                    {
                        Request.ReportDefinition.S3Region = Region;
                    }

                    if (Request.ReportDefinition.TimeUnit == null || String.IsNullOrEmpty(Request.ReportDefinition.TimeUnit.Value))
                    {
                        Request.ReportDefinition.TimeUnit = DefaultTimeUnit;
                    }

                    if (Request.ReportDefinition.AdditionalArtifacts != null &&
                       Request.ReportDefinition.AdditionalArtifacts.Any(x => x.Equals("ATHENA", StringComparison.OrdinalIgnoreCase))
                    )
                    {
                        if (Request.ReportDefinition.AdditionalArtifacts.Count > 1)
                        {
                            throw new InvalidOperationException("The additional artifact ATHENA cannot be combined with other values.");
                        }

                        if (Request.ReportDefinition.Format != ReportFormat.Parquet || Request.ReportDefinition.Compression != CompressionFormat.Parquet)
                        {
                            throw new InvalidOperationException("You must specify Parquet as the format and compression type when ATHENA is specified as the additional artifact.");
                        }
                    }
                    else if (Request.ReportDefinition.AdditionalArtifacts.Any(x => x.Equals("REDSHIFT", StringComparison.OrdinalIgnoreCase) || x.Equals("QUICKSIGHT", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (Request.ReportDefinition.Format != ReportFormat.TextORcsv || Request.ReportDefinition.Compression != CompressionFormat.GZIP)
                        {
                            throw new InvalidOperationException("You must specify TextORCsv as the format and GZIP as the compression type when REDSHIFT or QUICKSIGHT are specified as the additional artifacts.");
                        }
                    }
                    else
                    {
                        if (Request.ReportDefinition.Compression == null || String.IsNullOrEmpty(Request.ReportDefinition.Compression.Value))
                        {
                            Request.ReportDefinition.Compression = DefaultCompression;
                        }

                        if (Request.ReportDefinition.Format == null || String.IsNullOrEmpty(Request.ReportDefinition.Format.Value))
                        {
                            Request.ReportDefinition.Format = DefaultFormat;
                        }
                    }

                    PutReportDefinitionResponse Response = await Client.PutReportDefinitionAsync(Request);

                    if ((int)Response.HttpStatusCode < 200 || (int)Response.HttpStatusCode > 299)
                    {
                        return new CustomResourceResponse(
                            CustomResourceResponse.RequestStatus.FAILED, 
                            $"Received HTTP status code {(int)Response.HttpStatusCode}.", 
                            request
                        );
                    }
                    else
                    {
                        return new CustomResourceResponse(
                            CustomResourceResponse.RequestStatus.SUCCESS,
                            $"See the details in CloudWatch Log Stream: {context.LogStreamName}.",
                            Request.ReportDefinition.ReportName,
                            request.StackId,
                            request.RequestId,
                            request.LogicalResourceId,
                            false,
                            new Dictionary<string, object>()
                            {
                                {"Name", Request.ReportDefinition.ReportName },
                                {"Arn", $"arn:aws:cur:{Region}:{AccountId}:definition/{ReportName}" },
                                {"Id", Request.ReportDefinition.ReportName }
                            }
                        );
                    }
                }
                catch (AmazonCostAndUsageReportException e)
                {
                    context.LogError(e);

                    return new CustomResourceResponse(
                        CustomResourceResponse.RequestStatus.FAILED,
                        e.Message,
                        ReportName,
                        request.StackId,
                        request.RequestId,
                        request.LogicalResourceId
                    );
                }
                catch (Exception e)
                {
                    context.LogError(e);

                    return new CustomResourceResponse(
                        CustomResourceResponse.RequestStatus.FAILED,
                        e.Message,
                        ReportName,
                        request.StackId,
                        request.RequestId,
                        request.LogicalResourceId
                    );
                }
            };
            
            Func<CustomResourceRequest, ILambdaContext, Task<CustomResourceResponse>> DeleteAsync = async (request, context) =>
            {
                try
                {
                    //arn:aws:lambda:us-east-1:123456789012:function:FunctionName
                    string[] Parts = context.InvokedFunctionArn.Split(":");
                    string Region = Parts[3];
                    string AccountId = Parts[4];

                    AmazonCostAndUsageReportConfig Config = new AmazonCostAndUsageReportConfig();
                    IAmazonCostAndUsageReport Client = new AmazonCostAndUsageReportClient(Config);

                    DeleteReportDefinitionRequest Request = new DeleteReportDefinitionRequest()
                    {
                        ReportName = request.PhysicalResourceId
                    };

                    DeleteReportDefinitionResponse Response = await Client.DeleteReportDefinitionAsync(Request);

                    if ((int)Response.HttpStatusCode < 200 || (int)Response.HttpStatusCode > 299)
                    {
                        return new CustomResourceResponse(CustomResourceResponse.RequestStatus.FAILED, $"Received HTTP status code {(int)Response.HttpStatusCode}.", request);
                    }
                    else
                    {
                        return new CustomResourceResponse(
                            CustomResourceResponse.RequestStatus.SUCCESS,
                            $"See the details in CloudWatch Log Stream: {context.LogStreamName}.",
                            request.PhysicalResourceId,
                            request.StackId,
                            request.RequestId,
                            request.LogicalResourceId,
                            false,
                            new Dictionary<string, object>()
                            {
                                {"Name", request.PhysicalResourceId as string },
                                {"Arn", $"arn:aws:cur:{Region}:{AccountId}:definition/{request.PhysicalResourceId}" },
                                {"Id", request.PhysicalResourceId as string }
                            }
                        );
                    }
                }
                catch (AmazonCostAndUsageReportException e)
                {
                    context.LogError(e);

                    return new CustomResourceResponse(
                        CustomResourceResponse.RequestStatus.FAILED,
                        e.Message,
                        request.PhysicalResourceId,
                        request.StackId,
                        request.RequestId,
                        request.LogicalResourceId
                    );
                }
                catch (Exception e)
                {
                    context.LogError(e);

                    return new CustomResourceResponse(
                        CustomResourceResponse.RequestStatus.FAILED,
                        e.Message,
                        request.PhysicalResourceId,
                        request.StackId,
                        request.RequestId,
                        request.LogicalResourceId
                    );
                }
            };

            Func<CustomResourceRequest, ILambdaContext, Task<CustomResourceResponse>> UpdateAsync = async (request, context) =>
            {
                CustomResourceResponse Response = await DeleteAsync(request, context);

                if (Response.Status == CustomResourceResponse.RequestStatus.SUCCESS)
                {
                    return await CreateAsync(request, context);
                }
                else
                {
                    return Response;
                }
            };

            this._Handler = new CustomResourceFactory(CreateAsync, UpdateAsync, DeleteAsync);

            this.ProcessEnvironmentVariables();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Entrypoint for the Lambda function, calls the correct create, update, or delete function
        /// </summary>
        /// <param name="request">The custom resource request</param>
        /// <param name="context">The ILambdaContext object</param>
        /// <returns></returns>
        public async Task Execute(CustomResourceRequest request, ILambdaContext context)
        {
            context.LogInfo($"Received request:\n{JsonConvert.SerializeObject(request)}");

            CustomResourceResult Result = await this._Handler.ExecuteAsync(request, context);

            if (Result.IsSuccess)
            {
                context.LogInfo("Successfully ran custom resource handler.");
            }
            else
            {
                context.LogError("Custom resource handler failed to run successfully.");
            }          
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Processes the environment variables supplied to the lambda function
        /// </summary>
        private void ProcessEnvironmentVariables()
        {
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DefaultFormat")))
            {
                ReportFormat Temp = ReportFormat.FindValue(Environment.GetEnvironmentVariable("DefaultFormat"));

                if (Temp.IsStaticReadOnlyFieldOfClass())
                {
                    DefaultFormat = Temp;
                }
            }

            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DefaultCompression")))
            {
                CompressionFormat Temp = CompressionFormat.FindValue(Environment.GetEnvironmentVariable("DefaultCompression"));

                if (Temp.IsStaticReadOnlyFieldOfClass())
                {
                    DefaultCompression = Temp;
                }
            }

            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DefaultTimeUnit")))
            {
                TimeUnit Temp = TimeUnit.FindValue(Environment.GetEnvironmentVariable("DefaultTimeUnit"));

                if (Temp.IsStaticReadOnlyFieldOfClass())
                {
                    DefaultTimeUnit = Temp;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Internally used extension methods
    /// </summary>
    internal static class ExtensionMethods
    {
        /// <summary>
        /// This method can be used as a test to check if an object is defined in its class as a static read only field. This is
        /// common where "constant" values of the class are defined like an enum. For example, take this class:
        /// 
        /// public class Format {
        /// 
        ///     prviate string value;
        /// 
        ///     public static readonly Format CSV = new Format("csv");
        /// 
        ///     public Format(string val) 
        ///     {
        ///         this.value = val;
        ///     }
        /// }
        /// 
        /// We may want to check if a Format object we have is defined as a static readonly field of the same type as
        /// the containing class. In that case:
        /// 
        /// Format text = new Format("text");
        /// bool result = text.IsStaticReadOnlyFieldOfClass();
        /// 
        /// The result would be false in this case.
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="flags">Additional flags</param>
        /// <returns></returns>
        public static bool IsStaticReadOnlyFieldOfClass<T>(this T value, BindingFlags flags = BindingFlags.Default) where T : class
        {
            bool Success = false;

            if (flags == BindingFlags.Default)
            {
                flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            }

            IEnumerable<FieldInfo> Fields = typeof(T).GetFields(flags).Where(x => x.IsInitOnly && x.FieldType == typeof(T));

            foreach (FieldInfo Field in Fields)
            {
                T Val = Field.GetValue(null) as T;

                if (Val == value)
                {
                    Success = true;
                    break;
                }
            }

            return Success;
        }
    }
}
