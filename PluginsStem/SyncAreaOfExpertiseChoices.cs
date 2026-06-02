using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using System.Web.Services.Description;

namespace PluginsStem
{
    public class SyncAreaOfExpertiseChoices
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(context.UserId);
                ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                Entity entity = (Entity)context.InputParameters["Target"];
                var entityID = entity.Id;

                if (entity.LogicalName == "amb_areaofexpertise")
                {
                    if (context.MessageName == "Create" && entity.Contains("amb_sqlid") && entity["amb_sqlid"] != null)
                    {

                        int sqlId = entity.GetAttributeValue<int>("amb_sqlid");
                        string name = entity.GetAttributeValue<string>("name");

                        var request = new InsertOptionValueRequest
                        {
                            OptionSetName = "amb_areaofexpertise",
                            Label = new Label(name, 1033),
                            Value = sqlId
                        };

                        orgService.Execute(request);

                        var publish = new PublishXmlRequest
                        {
                            ParameterXml = $"<importexportxml><optionsets><optionset>{"amb_areaofexpertise"}</optionset></optionsets></importexportxml>"
                        };
                        orgService.Execute(publish);

                    }

                    if (context.MessageName == "Update" && entity.Contains("amb_sqlid") && entity["amb_sqlid"] != null)
                    {
                        if (entity.Contains("name")) // only if name changed
                        {
                            // Get sql id from either Target or PreImage
                            int sqlId = entity.Contains("amb_sqlid")
                                ? entity.GetAttributeValue<int>("amb_sqlid")
                                : context.PreEntityImages["PreImage"].GetAttributeValue<int>("amb_sqlid");

                            string newName = entity.GetAttributeValue<string>("name");

                            var updateRequest = new UpdateOptionValueRequest
                            {
                                OptionSetName = "amb_areaofexpertise",
                                Value = sqlId,
                                Label = new Label(newName, 1033)
                            };

                            orgService.Execute(updateRequest);

                            var publish = new PublishXmlRequest
                            {
                                ParameterXml = $"<importexportxml><optionsets><optionset>{"amb_areaofexpertise"}</optionset></optionsets></importexportxml>"
                            };
                            orgService.Execute(publish);

                        }
                    }

                    if (context.MessageName == "Delete" && entity.Contains("amb_sqlid") && entity["amb_sqlid"] != null)
                    {
                        // Get sql id from pre-image
                        var preImage = context.PreEntityImages["PreImage"];
                        int sqlId = preImage.GetAttributeValue<int>("amb_sqlid");

                        var deleteRequest = new DeleteOptionValueRequest
                        {
                            OptionSetName = "amb_areaofexpertise",
                            Value = sqlId
                        };

                        orgService.Execute(deleteRequest);

                        var publish = new PublishXmlRequest
                        {
                            ParameterXml = $"<importexportxml><optionsets><optionset>{"amb_areaofexpertise"}</optionset></optionsets></importexportxml>"
                        };
                        orgService.Execute(publish);

                    }

                }

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in SyncAreaOfExpertiseChoices " + ex.Message);
            }
        }


    }
}
