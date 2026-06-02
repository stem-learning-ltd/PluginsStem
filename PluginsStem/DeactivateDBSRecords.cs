using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class DeactivateDBSRecords: IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(context.UserId);
                ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                if(context.Depth > 1)
                {
                    return;
                }

                Entity entity = (Entity)context.InputParameters["Target"];
                
                //var entityID = entity.Id;

                if (context.MessageName == "Create")
                {
                    var contactId = entity.GetAttributeValue<EntityReference>("sam_dbscontact").Id;

                    updateDBSRecords(orgService, contactId);
                }
                else if (context.MessageName == "Update")
                {
                    var preImage = context.PreEntityImages?["PreImage"];
                    var contactId = preImage.GetAttributeValue<EntityReference>("sam_dbscontact").Id;

                    updateDBSRecords(orgService, contactId);
                }
                else
                {
                    return;
                }

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in DeactivateDBSRecords Plugin: " + ex.Message);
            }
        }

        public void updateDBSRecords(IOrganizationService orgService, Guid contactID)
        {

            var recordsDBS = getActiveDBSRecords(orgService, contactID);

            if (recordsDBS.Count == 0)
            {
                return;
            }

            var latestRecord = recordsDBS.First();

            foreach (var record in recordsDBS.Skip(1))
            {
                var dbsToUpdate = new Entity("sam_dbs");
                dbsToUpdate.Id = record.Id;

                var currentStatus = record.GetAttributeValue<OptionSetValue>("statuscode");

                var setState = new OrganizationRequest("SetState")
                {
                    ["EntityMoniker"] = new EntityReference("sam_dbs", record.Id),
                    ["State"] = new OptionSetValue(1),
                    ["Status"] = new OptionSetValue(999990010)
                };

                orgService.Execute(setState);

                var contactToUpdate = new Entity("contact");
                contactToUpdate.Id = record.GetAttributeValue<EntityReference>("sam_dbscontact").Id;

                contactToUpdate["sam_dbsstatus"] = new OptionSetValue(6);
                orgService.Update(contactToUpdate);

            }

        }

        public List<Entity> getActiveDBSRecords(IOrganizationService orgService, Guid ID)
        {
            EntityCollection results = new EntityCollection();
            EntityCollection resultsTemp = new EntityCollection();


            try
            {
                var query_statecode = 0;

                var query = new QueryExpression("sam_dbs");

                query.ColumnSet.AddColumns("sam_dbsexpirydate", "statuscode", "sam_dbscontact");

                query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);
                query.Criteria.AddCondition("sam_dbscontact", ConditionOperator.Equal, ID);

                query.AddOrder("createdon", OrderType.Descending);

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
                throw new InvalidPluginExecutionException("Error in getActiveDBSRecords function, Plugin: DeactivateDBSRecords " + ex.Message);
            }


            return results.Entities.ToList();
        }

    }
}
