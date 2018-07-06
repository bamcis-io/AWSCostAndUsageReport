# BAMCIS AWS Cost and Usage Report Custom Resource

This provides an AWS Lambda function that can be used as a custom resource in a  CloudFormation script to create, update, and delete AWS Cost and Usage Reports.

## Table of Contents
- [Usage](#usage)
  * [Required Properties](#required-properties)
  * [Default Values](#default-values)
- [Revision History](#revision-history)

## Usage

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
                "Compression" : "GZIP",
                "Format"      : "TextORcsv",
                "S3Bucket"    : {
                    "Ref" : "ReportDeliveryBucket"
                },
                "S3Prefix"    : {
                    "Fn::Join" : [
                        "",
                        [
                            {
                                "Ref" : "AWS::AccountId"
                            },
                            "/"
                        ]
                    ]
                },
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

## Revision History

### 1.0.0
Initial release of the application.
