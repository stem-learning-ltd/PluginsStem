using System;
using Microsoft.Xrm.Sdk;

namespace PluginsStem
{
    public class CalculateActualDuration : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(context.UserId);
                ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                    return;

                Entity target = (Entity)context.InputParameters["Target"];

                if (target.LogicalName != "sam_ambassadoractivity")
                    return;

                if (context.MessageName == "Create" || context.MessageName == "Update")
                {
                    Entity preImage = null;

                    // 🔥 NEW — get PreImage
                    if (context.PreEntityImages.Contains("PreImage"))
                    {
                        preImage = context.PreEntityImages["PreImage"];
                    }

                    CalculateDuration(orgService, target, preImage);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in CalculateActualDuration Plugin: " + ex.Message);
            }
        }

        public void CalculateDuration(IOrganizationService orgService, Entity target, Entity preImage)
        {
            DateTime? actualStart =
                target.Contains("actualstart")
                ? target.GetAttributeValue<DateTime?>("actualstart")
                : preImage?.GetAttributeValue<DateTime?>("actualstart");

            DateTime? actualEnd =
                target.Contains("actualend")
                ? target.GetAttributeValue<DateTime?>("actualend")
                : preImage?.GetAttributeValue<DateTime?>("actualend");

            if (actualStart == null || actualEnd == null)
                return;

            int durationMinutes = (int)(actualEnd.Value - actualStart.Value).TotalMinutes;

            Entity updateActivity = new Entity("sam_ambassadoractivity");
            updateActivity.Id = target.Id;
            updateActivity["actualdurationminutes"] = durationMinutes;

            orgService.Update(updateActivity);
        }
    }
}