using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class ContactSyncDBSFields: IPlugin
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

                if(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
                {
                    if (context.MessageName == "Create")
                    {
                        var dbsRecord = orgService.Retrieve("sam_dbs", entityID, new ColumnSet("sam_dbscontact"));

                        var contactID = dbsRecord.GetAttributeValue<EntityReference>("sam_dbscontact").Id;

                        updateContact(orgService, contactID);
                    }

                    else if (context.MessageName == "Update")
                    {
                        var dbsRecord = orgService.Retrieve("sam_dbs", entityID, new ColumnSet("sam_dbscontact"));

                        var contactID = dbsRecord.GetAttributeValue<EntityReference>("sam_dbscontact").Id;

                        updateContact(orgService, contactID);

                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }

                

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in ContactSyncDBSFields Plugin: " + ex.Message);
            }
        }

        public void updateContact(IOrganizationService orgService, Guid ID)
        {
            var contactToUpdate = new Entity("contact");
            contactToUpdate.Id = ID;

            var recordDBS = getActiveDBSRecords(orgService, ID);

            if (recordDBS != null)
            {
                var dbsStatusValue = recordDBS.GetAttributeValue<OptionSetValue>("statuscode").Value;

                OptionSetValue contactDBSStatus = null;

                switch (dbsStatusValue)
                {
                    case 999990002:
                        contactDBSStatus = new OptionSetValue(6);
                        break;

                    case 999990004:
                        contactDBSStatus = new OptionSetValue(1);
                        break;

                    case 999990005:
                        contactDBSStatus = new OptionSetValue(1);
                        break;

                    case 999990006:
                        contactDBSStatus = new OptionSetValue(2);
                        break;

                    case 999990007:
                        contactDBSStatus = new OptionSetValue(3);
                        break;

                    case 999990008:
                        contactDBSStatus = new OptionSetValue(4);
                        break;

                    case 999990009:
                        contactDBSStatus = new OptionSetValue(5);
                        break;

                }

                contactToUpdate["sam_dbsstatus"] = contactDBSStatus;
                contactToUpdate["sam_dbscertificatenumber"] = recordDBS["sam_certificatenumber"];
                contactToUpdate["sam_dbsexpirydate"] = recordDBS["sam_dbsexpirydate"];
                contactToUpdate["sam_idcardexpirydate"] = recordDBS["sam_dbsexpirydate"];
                                
                orgService.Update(contactToUpdate);
            }

            else
            {
                return;
            }
           
        }

        public Entity getActiveDBSRecords(IOrganizationService orgService, Guid ID)
        {
            EntityCollection results = new EntityCollection();
            EntityCollection resultsTemp = new EntityCollection();


            try
            {
                var query_statecode = 0;

                var query = new QueryExpression("sam_dbs");

                query.ColumnSet.AddColumns("sam_certificatenumber", "sam_dbsexpirydate", "statuscode");

                query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
                query.Criteria.AddCondition("sam_dbscontact", ConditionOperator.Equal, ID);
                
                query.PageInfo = new PagingInfo()
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                do
                {
                    resultsTemp = orgService.RetrieveMultiple(query);                 
                    results.Entities.AddRange(resultsTemp.Entities);
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = resultsTemp.PagingCookie;
                }
                while (resultsTemp.MoreRecords);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in getActiveDBSRecords function, Plugin: ContactSyncDBSFields " + ex.Message);
            }


            return results.Entities.FirstOrDefault();
        }

    }
}
