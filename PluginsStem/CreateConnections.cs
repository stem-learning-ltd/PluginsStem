using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace PluginsStem
{
    public class CreateConnections : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            string recordId = context.InputParameters["recordID"] as string;
            string accountIdsJson = context.InputParameters["accountIDs"] as string;
            string accountType = context.InputParameters.Contains("accountType") ? context.InputParameters["accountType"] as string : null;

            var accounts = JsonConvert.DeserializeObject<List<AccountDto>>(accountIdsJson); // 🔄 CHANGED
            Guid regardingRecord = Guid.Parse(recordId);

            foreach (var acc in accounts)
            {
                Entity connection = new Entity("connection");
                connection["record2id"] = new EntityReference("account", acc.id);
                connection["record1id"] = new EntityReference("sam_ambassadoractivity", regardingRecord);
                connection["description"] = "Created from custom popup";
                connection["effectivestart"] = DateTime.UtcNow;

                // 🔥 NEW
                connection["sam_contacttype"] = new OptionSetValue(acc.isAdmin ? 2 : 1);

                switch (accountType)
                {
                    case "999990001":
                        connection["sam_type"] = new OptionSetValue(3);
                        break;

                    case "157430004":
                        connection["sam_type"] = new OptionSetValue(5);
                        break;

                    case "157430000":
                        connection["sam_type"] = new OptionSetValue(4);
                        break;
                }

                service.Create(connection);
            }
        }
    }
}