using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class PopulateActivityAmbassadorName : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!context.InputParameters.Contains("Target"))
                return;

            var target = context.InputParameters["Target"] as Entity;
            if (target == null || target.LogicalName != "sam_activityambassador")
                return;

            if (context.Depth > 1)
                return;

            Entity preImage = null;
            if (context.MessageName == "Update" &&
                context.PreEntityImages != null &&
                context.PreEntityImages.Contains("PreImage"))
            {
                preImage = context.PreEntityImages["PreImage"];
            }

            EntityReference ambassadorRef =
                target.GetAttributeValue<EntityReference>("sam_ambassador") ??
                preImage?.GetAttributeValue<EntityReference>("sam_ambassador");

            EntityReference activityRef =
                target.GetAttributeValue<EntityReference>("sam_ambassadoractivity") ??
                preImage?.GetAttributeValue<EntityReference>("sam_ambassadoractivity");

            if (ambassadorRef == null || activityRef == null)
            {
                trace.Trace("PopulateActivityAmbassadorName: Missing ambassador or activity reference");
                return;
            }

            string ambassadorName = ambassadorRef.Name;
            string activitySubject = activityRef.Name;

            if (string.IsNullOrWhiteSpace(ambassadorName))
            {
                var contact = service.Retrieve("contact", ambassadorRef.Id,
                    new ColumnSet("fullname"));
                ambassadorName = contact.GetAttributeValue<string>("fullname") ?? "";
            }

            if (string.IsNullOrWhiteSpace(activitySubject))
            {
                var act = service.Retrieve("sam_ambassadoractivity", activityRef.Id,
                    new ColumnSet("subject"));
                activitySubject = act.GetAttributeValue<string>("subject") ?? "";
            }

            string combined = $"{ambassadorName} - {activitySubject}".Trim();

            trace.Trace("Setting sam_name to: " + combined);

            target["sam_name"] = combined;
            service.Update(target);
        }
    }
}
