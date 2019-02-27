# BAMCIS AWS Cost and Usage Report Custom Resource

This provides an AWS Lambda function that can be used as a custom resource in a 
CloudFormation script to create, update, and delete AWS Cost and Usage Reports.

## Table of Contents
- [Usage](#usage)
  * [Required Properties](#required-properties)
  * [Default Values](#default-values)
  * [Formatting](#formatting)
- [Revision History](#revision-history)

## Usage

Deploy the Lambda function using the included `AWSCostAndUsageReport.template` CloudFormation script. If you are using Visual Studio with the AWS Toolkit installed, you can deploy directly from the IDE. Otherwise follow the instructions [here](#https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-how-to-create-deployment-package.html) or [here](#https://docs.aws.amazon.com/toolkit-for-visual-studio/latest/user-guide/lambda-cli-publish.html) for creating a deployment package and uploading it to S3.

Once you deploy the Lambda function, take note of its Arn. Supply this as the service token for the custom resource. The properties of the resource will by the JSON representation of a `PutReportDefinitionRequest` object. It should have a single top level property, `ReportDefinition`, and then several key value pairs under that property. For example, your custom resource might look like:

    "CostAndUsageReport"   : {
        "Type" : "Custom::CUR",
        "Properties" : {
            "ServiceToken" : {
                "Ref" : "CostAndUsageReportLambdaArn"
            },
            "ReportDefinition" : {
                "ReportName" : {
                    "Ref" : "ReportName"
                },
                "AdditionalSchemaElements" : [
                    "RESOURCES"
                ],
                "AdditionalArtifacts" : [
                    "QUICKSIGHT"
                ],
                "Compression" : "GZIP",
                "Format"      : "TextORcsv",
                "S3Bucket"    : {
                    "Ref" : "ReportDeliveryBucket"
                },
                "S3Prefix"    : {
                    "Fn::Sub" : "${AWS::AccountId}/"
                },
                "RefreshClosedReports" : "true",
                "ReportVersioning" : "OVERWRITE_REPORT",
                "S3Region"      : {
                    "Ref" : "AWS::Region"
                },
                "TimeUnit"    : {
                    "Ref" : "Frequency"
                }
            }
        },
        "DependsOn"  : [
            "BucketPolicy"
        ]
    }

A few notes, the S3 bucket must have a very specific bucket policy applied to it. Check AWS documentation for the policy, you can also see the policy in the unit test CloudFormation template. If you create the S3 bucket and CUR in the same CloudFormation template, ensure the CUR resource depends on the bucket policy so that it doesn't fail the permissions check.

### Required Properties

Required properties in ReportDefinition:
- ReportName
- S3Bucket

### Default Values

These properties will default to specified values if not specified in the CloudFormation template
- S3Region : Defaults to the region the Lambda function is deployed in
- Format : Defaults to `TextORcsv`
- TimeUnit : Defaults to `DAILY`
- Compression : Defaults to `GZIP`

I recommend you stick with GZIP for compression (I found that the ZIP files could not be uncompressed by other AWS Services like AWS Glue). 

### Formatting
If you choose `"ATHENA"` as one of the `AdditionalArtifacts` members, this is the only member you can define. You must also set the format and compression values to `parquet`. 

If you choose `"QUICKSIGHT"` and/or `"REDSHIFT"` you must select `TextORcsv` as the format and `GZIP` as the compression.

## Revision History

### 1.1.0
Added support for Parquet format/compression. Allowed changing the default values through environment variables.

### 1.0.0
Initial release of the application.