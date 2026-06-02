using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class CalculateInductionStatus : IPlugin
    {
        private const string FIELD_SAFEGUARDING_COMPLETED = "sam_safeguardingcoursecompleted";
        private const string FIELD_VIDEO_WATCHED = "sam_inductionvideowatched";
        private const string FIELD_AGREEMENT_SIGNED = "sam_ambassadoragreementsigned";
        private const string FIELD_PROFILE_COMPLETED = "sam_profilecompleted";

        private const string FIELD_INDUCTION_STATUS = "sam_inductionstatus";
        private const string FIELD_INDUCTION_DATE = "stem_inductiondate";

        private const int INDUCTION_STATUS_COMPLETE = 1;
        private const int INDUCTION_STATUS_INCOMPLETE = 2;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            var serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var service =
                serviceFactory.CreateOrganizationService(context.UserId);

            var trace =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.MessageName != "Create" &&
                    context.MessageName != "Update")
                    return;

                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;

                if (target == null || target.LogicalName != "contact")
                    return;

                var contactId = target.Id;

                // =========================
                // GET CURRENT STATE
                // =========================
                var contact = service.Retrieve(
                    "contact",
                    contactId,
                    new ColumnSet(
                        FIELD_SAFEGUARDING_COMPLETED,
                        FIELD_VIDEO_WATCHED,
                        FIELD_AGREEMENT_SIGNED,
                        FIELD_PROFILE_COMPLETED,
                        FIELD_INDUCTION_DATE,
                        FIELD_INDUCTION_STATUS));

                int existingStatus =
                    contact.GetAttributeValue<OptionSetValue>(FIELD_INDUCTION_STATUS)?.Value ?? 0;

                DateTime? existingDate =
                    contact.GetAttributeValue<DateTime?>(FIELD_INDUCTION_DATE);

                bool safeguardingCompleted =
                    contact.GetAttributeValue<bool>(FIELD_SAFEGUARDING_COMPLETED);

                bool videoWatched =
                    contact.GetAttributeValue<bool>(FIELD_VIDEO_WATCHED);

                bool agreementSigned =
                    contact.GetAttributeValue<bool>(FIELD_AGREEMENT_SIGNED);

                bool profileCompleted =
                    contact.GetAttributeValue<bool>(FIELD_PROFILE_COMPLETED);

                trace?.Trace(
                    "CalculateInductionStatus: safeguarding={0}, video={1}, agreement={2}, profile={3}",
                    safeguardingCompleted,
                    videoWatched,
                    agreementSigned,
                    profileCompleted);

                // =========================
                // CALCULATE NEW STATE
                // =========================
                bool isComplete =
                    safeguardingCompleted &&
                    videoWatched &&
                    agreementSigned &&
                    profileCompleted;

                int newStatus =
                    isComplete
                        ? INDUCTION_STATUS_COMPLETE
                        : INDUCTION_STATUS_INCOMPLETE;

                // =========================
                // BUILD UPDATE
                // =========================
                var update = new Entity("contact")
                {
                    Id = contactId
                };

                update[FIELD_INDUCTION_STATUS] =
                    new OptionSetValue(newStatus);

                if (newStatus == INDUCTION_STATUS_COMPLETE)
                {
                    if (!existingDate.HasValue)
                    {
                        update[FIELD_INDUCTION_DATE] = DateTime.UtcNow;
                        trace?.Trace("Induction date SET");
                    }
                }
                else
                {
                    if (existingDate.HasValue)
                    {
                        update[FIELD_INDUCTION_DATE] = null;
                        trace?.Trace("Induction date CLEARED");
                    }
                }

                // =========================
                // CHANGE DETECTION (IMPORTANT)
                // =========================
                bool needsUpdate =
                    existingStatus != newStatus ||
                    (newStatus == INDUCTION_STATUS_COMPLETE && !existingDate.HasValue) ||
                    (newStatus == INDUCTION_STATUS_INCOMPLETE && existingDate.HasValue);

                if (!needsUpdate)
                {
                    trace?.Trace("No induction update required.");
                    return;
                }

                // =========================
                // EXECUTE UPDATE
                // =========================
                service.Update(update);

                trace?.Trace("Induction status updated to {0}", newStatus);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "CalculateInductionStatus failed: " + ex.Message, ex);
            }
        }
    }
}