using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class AmbassadorFillEmailAddress: IPlugin
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

                if(entity.LogicalName == "amb_ambassador")
                {
                    if(context.MessageName == "Create" && entity.Contains("amb_userid") && entity["amb_userid"] != null)
                    {
                        var ambassadorUserID = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var ambassadorUserEntity = orgService.Retrieve("amb_user", ambassadorUserID, new ColumnSet("amb_emailaddress"));

                        var ambassadorToUpdate = new Entity("amb_ambassador");
                        ambassadorToUpdate.Id = entity.Id;

                        ambassadorToUpdate["amb_emailaddress"] = ambassadorUserEntity["amb_emailaddress"];
                        orgService.Update(ambassadorToUpdate);

                    }

                    if (context.MessageName == "Update" && entity.Contains("amb_userid") && entity["amb_userid"] != null)
                    {
                        var ambassadorUserID = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var ambassadorUserEntity = orgService.Retrieve("amb_user", ambassadorUserID, new ColumnSet("amb_emailaddress"));

                        var ambassadorToUpdate = new Entity("amb_ambassador");
                        ambassadorToUpdate.Id = entity.Id;

                        ambassadorToUpdate["amb_emailaddress"] = ambassadorUserEntity["amb_emailaddress"];
                        orgService.Update(ambassadorToUpdate);

                    }

                }

                if (entity.LogicalName == "contact")
                {
                    if (context.MessageName != "Create" && context.MessageName != "Update")
                        return;

                    string email = null;
                    if (entity.Contains("emailaddress1"))
                        email = entity.GetAttributeValue<string>("emailaddress1");

                    if (string.IsNullOrWhiteSpace(email))
                        return;

                    var contactId = entity.Id;

                    // 1) Find amb_user records linked to this contact via amb_ambassador_contact
                    var userQuery = new QueryExpression("amb_user")
                    {
                        ColumnSet = new ColumnSet("amb_userid"),
                        Criteria = new FilterExpression(LogicalOperator.And)
                    };
                    userQuery.Criteria.AddCondition("amb_ambassador_contact", ConditionOperator.Equal, contactId);

                    var users = orgService.RetrieveMultiple(userQuery);

                    if (users.Entities.Count == 0)
                        return;

                    // 2) For each user, find ambassadors linked via amb_userid and update email
                    foreach (var user in users.Entities)
                    {
                        var userId = user.Id;

                        var ambQuery = new QueryExpression("amb_ambassador")
                        {
                            ColumnSet = new ColumnSet("amb_ambassadorid", "amb_emailaddress"),
                            Criteria = new FilterExpression(LogicalOperator.And)
                        };
                        ambQuery.Criteria.AddCondition("amb_userid", ConditionOperator.Equal, userId);

                        var ambassadors = orgService.RetrieveMultiple(ambQuery);

                        foreach (var amb in ambassadors.Entities)
                        {
                            var current = amb.GetAttributeValue<string>("amb_emailaddress");
                            if (string.Equals(current, email, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var ambToUpdate = new Entity("amb_ambassador");
                            ambToUpdate.Id = amb.Id;
                            ambToUpdate["amb_emailaddress"] = email;
                            orgService.Update(ambToUpdate);
                        }
                    }
                }



                if (entity.LogicalName == "amb_user")
                {
                    if (context.MessageName != "Create" && context.MessageName != "Update")
                        return;

                    // Get current email (from Target or PreImage on Update)
                    string newEmail = null;

                    if (entity.Contains("amb_emailaddress"))
                        newEmail = entity.GetAttributeValue<string>("amb_emailaddress");

                    if (context.MessageName == "Update" && newEmail == null)
                    {
                        if (context.PreEntityImages != null && context.PreEntityImages.Contains("PreImage"))
                        {
                            var pre = context.PreEntityImages["PreImage"];
                            newEmail = pre.GetAttributeValue<string>("amb_emailaddress");
                        }
                    }

                    // Nothing to sync
                    if (string.IsNullOrWhiteSpace(newEmail))
                        return;

                    var userId = entity.Id;

                    // Find ambassadors linked to this user
                    var q = new QueryExpression("amb_ambassador")
                    {
                        ColumnSet = new ColumnSet("amb_ambassadorid", "amb_emailaddress"),
                        Criteria = new FilterExpression(LogicalOperator.And)
                    };
                    q.Criteria.AddCondition("amb_userid", ConditionOperator.Equal, userId);

                    var ambassadors = orgService.RetrieveMultiple(q);

                    foreach (var amb in ambassadors.Entities)
                    {
                        // Optional: avoid update if already same
                        var currentEmail = amb.GetAttributeValue<string>("amb_emailaddress");
                        if (string.Equals(currentEmail, newEmail, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var ambToUpdate = new Entity("amb_ambassador");
                        ambToUpdate.Id = amb.Id;
                        ambToUpdate["amb_emailaddress"] = newEmail;
                        orgService.Update(ambToUpdate);
                    }
                }



            }
            catch (Exception ex) 
            {
                throw new InvalidPluginExecutionException("Error in AmbassadorFillEmailAddress " + ex.Message);
            }
        }

    }

}
