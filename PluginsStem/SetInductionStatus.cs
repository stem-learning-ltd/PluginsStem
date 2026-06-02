using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class SetInductionStatus : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context =
                    (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                IOrganizationService orgService =
                    serviceFactory.CreateOrganizationService(context.UserId);

                ITracingService tracer =
                    (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                if (context.MessageName != "Update")
                    return;

                if (!context.InputParameters.Contains("Target"))
                    return;

                Entity entity = (Entity)context.InputParameters["Target"];

                var entityId = entity.Id;

                // 1. induction status calculation
                if (entity.Contains("sam_safeguardingcoursecompleted") ||
                    entity.Contains("sam_inductionvideowatched") ||
                    entity.Contains("sam_ambassadoragreementsigned") ||
                    entity.Contains("sam_profilecompleted"))
                {
                    setInductionStatus(orgService, entityId);
                }

                // 2. induction date handling
                if (entity.Contains("sam_inductionstatus"))
                {
                    handleInductionDate(orgService, entityId, entity);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in SetInductionStatus Plugin: " + ex.Message
                );
            }
        }

        private void setInductionStatus(IOrganizationService orgService, Guid id)
        {
            var contact = orgService.Retrieve(
                "contact",
                id,
                new ColumnSet(
                    "sam_safeguardingcoursecompleted",
                    "sam_inductionvideowatched",
                    "sam_ambassadoragreementsigned",
                    "sam_profilecompleted"
                )
            );

            var safeguardingCourseCompleted = contact.GetAttributeValue<bool>("sam_safeguardingcoursecompleted");
            var inductionVideoWatched = contact.GetAttributeValue<bool>("sam_inductionvideowatched");
            var ambassadorAgreementSigned = contact.GetAttributeValue<bool>("sam_ambassadoragreementsigned");
            var profileCompleted = contact.GetAttributeValue<bool>("sam_profilecompleted");

            var contactToUpdate = new Entity("contact") { Id = id };

            if (safeguardingCourseCompleted &&
                inductionVideoWatched &&
                ambassadorAgreementSigned &&
                profileCompleted)
            {
                contactToUpdate["sam_inductionstatus"] = new OptionSetValue(1); // Complete
            }
            else
            {
                contactToUpdate["sam_inductionstatus"] = new OptionSetValue(2); // Incomplete
            }

            orgService.Update(contactToUpdate);
        }

        private void handleInductionDate(IOrganizationService orgService, Guid id, Entity target)
        {
            var contact = orgService.Retrieve(
                "contact",
                id,
                new ColumnSet("stem_inductiondate", "sam_inductionstatus")
            );

            var status = target.GetAttributeValue<OptionSetValue>("sam_inductionstatus")
                         ?? contact.GetAttributeValue<OptionSetValue>("sam_inductionstatus");

            var existingDate = contact.GetAttributeValue<DateTime?>("stem_inductiondate");

            var update = new Entity("contact") { Id = id };

            // COMPLETE
            if (status != null && status.Value == 1)
            {
                if (!existingDate.HasValue)
                {
                    update["stem_inductiondate"] = DateTime.UtcNow;
                }
            }
            // INCOMPLETE
            else if (status != null && status.Value == 2)
            {
                update["stem_inductiondate"] = null;
            }

            if (update.Attributes.Count > 1)
            {
                orgService.Update(update);
            }
        }
    }
}