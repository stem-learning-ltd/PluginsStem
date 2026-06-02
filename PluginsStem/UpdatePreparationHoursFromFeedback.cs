using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class UpdatePreparationHoursFromFeedback : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;
                if (target == null || target.LogicalName != "sam_activityfeedback")
                    return;

                if (context.Depth > 1)
                    return;

                if (context.MessageName != "Update")
                    return;

                Entity preImage = null;
                if (context.PreEntityImages != null && context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                int newStatus = GetStatusCode(target, preImage);
                int oldStatus = GetOldStatusCode(preImage);

                const int STATUS_COMPLETED = 999990002;
                if (newStatus != STATUS_COMPLETED || oldStatus == STATUS_COMPLETED)
                    return;

                UpdateActivityAmbassadorPreparation(service, trace, target, preImage);
                UpdateContactTotalPreparation(service, trace, target, preImage);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in UpdatePreparationHoursFromFeedback plugin: " + ex.Message, ex);
            }
        }

        private static int GetStatusCode(Entity target, Entity preImage)
        {
            var sc = target.GetAttributeValue<OptionSetValue>("statuscode");
            if (sc != null) return sc.Value;

            var pre = preImage?.GetAttributeValue<OptionSetValue>("statuscode");
            return pre?.Value ?? -1;
        }

        private static int GetOldStatusCode(Entity preImage)
        {
            var pre = preImage?.GetAttributeValue<OptionSetValue>("statuscode");
            return pre?.Value ?? -1;
        }

        private void UpdateActivityAmbassadorPreparation(
            IOrganizationService service,
            ITracingService trace,
            Entity target,
            Entity preImage)
        {
            var ambRef =
                target.GetAttributeValue<EntityReference>("sam_activityambassador") ??
                preImage?.GetAttributeValue<EntityReference>("sam_activityambassador");

            if (ambRef == null)
                return;

            decimal prepHours = 0; 

            // CHANGE: decimal instead of int
            if (target.Attributes.Contains("sam_preparationtimedecimal"))
            {
                prepHours = target.GetAttributeValue<decimal?>("sam_preparationtimedecimal") ?? 0;
            }
            else if (preImage != null && preImage.Attributes.Contains("sam_preparationtimedecimal"))
            {
                prepHours = preImage.GetAttributeValue<decimal?>("sam_preparationtimedecimal") ?? 0;
            }

            int prepMinutes = (int)(prepHours * 60);

            var ambToUpdate = new Entity("sam_activityambassador");
            ambToUpdate.Id = ambRef.Id;

            ambToUpdate["sam_preparationhours_decimal"] = ConvertMinutesToQuarterHours(prepMinutes);

            service.Update(ambToUpdate);
        }

        private void UpdateContactTotalPreparation(
            IOrganizationService service,
            ITracingService trace,
            Entity target,
            Entity preImage)
        {
            var contactRef =
                target.GetAttributeValue<EntityReference>("sam_contact") ??
                preImage?.GetAttributeValue<EntityReference>("sam_contact");

            if (contactRef == null)
                return;

            Guid contactId = contactRef.Id;

            List<Entity> completedFeedbacks = GetCompletedFeedbacksForContact(service, contactId);

            // 🔥 CHANGE: decimal sum
            decimal totalPrep = completedFeedbacks.Sum(fb =>
            {
                decimal prepValue = fb.GetAttributeValue<decimal?>("sam_preparationtimedecimal") ?? 0;

                // convert hours -> minutes first
                int prepMinutes = (int)(prepValue * 60);

                return ConvertMinutesToQuarterHours(prepMinutes);
            });

            var contactToUpdate = new Entity("contact");
            contactToUpdate.Id = contactId;

            contactToUpdate["sam_ambassadorpreparationhours_decimal"] = totalPrep;

            service.Update(contactToUpdate);
        }

        private List<Entity> GetCompletedFeedbacksForContact(IOrganizationService orgService, Guid contactId)
        {
            var results = new EntityCollection();
            var resultsTemp = new EntityCollection();

            const int STATUS_COMPLETED = 999990002;

            var query = new QueryExpression("sam_activityfeedback");

            query.ColumnSet = new ColumnSet("sam_preparationtime","sam_preparationtimedecimal", "statuscode", "sam_contact");

            query.Criteria.AddCondition("sam_contact", ConditionOperator.Equal, contactId);
            query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, STATUS_COMPLETED);

            query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };

            do
            {
                resultsTemp = orgService.RetrieveMultiple(query);
                results.Entities.AddRange(resultsTemp.Entities);
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = resultsTemp.PagingCookie;
            }
            while (resultsTemp.MoreRecords);

            return results.Entities.ToList();
        }
        private decimal ConvertMinutesToQuarterHours(int minutes)
        {
            if (minutes <= 0)
                return 0m;

            decimal hours = minutes / 60m;

            // round to nearest 0.25
            return Math.Round(hours * 4, 0) / 4m;
        }
    }
}