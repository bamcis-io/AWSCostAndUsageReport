using Amazon.CostAndUsageReport;
using Amazon.CostAndUsageReport.Model;
using Amazon.Lambda.Core;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSLambda.Common.CustomResources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWSCostAndUsageReport
{
    public class Entrypoint
    {
        #region Private Fields

        /// <summary>
        /// The client handler for creating custom resources
        /// </summary>
        private ICustomResourceHandler _Handler;

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

                    // This is required
                    if (!Request.ReportDefinition.AdditionalSchemaElements.Contains("RESOURCES"))
                    {
                        Request.ReportDefinition.AdditionalSchemaElements.Add("RESOURCES");
                    }

                    // Setup defaults for the definition
                    if (String.IsNullOrEmpty(Request.ReportDefinition.S3Region))
                    {
                        Request.ReportDefinition.S3Region = Region;
                    }

                    if (Request.ReportDefinition.Compression == null || String.IsNullOrEmpty(Request.ReportDefinition.Compression.Value))
                    {
                        Request.ReportDefinition.Compression = CompressionFormat.GZIP;
                    }

                    if (Request.ReportDefinition.TimeUnit == null || String.IsNullOrEmpty(Request.ReportDefinition.TimeUnit.Value))
                    {
                        Request.ReportDefinition.TimeUnit = TimeUnit.DAILY;
                    }

                    if (Request.ReportDefinition.Format == null || String.IsNullOrEmpty(Request.ReportDefinition.Format.Value))
                    {
                        Request.ReportDefinition.Format = ReportFormat.TextORcsv;
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
    }
}
