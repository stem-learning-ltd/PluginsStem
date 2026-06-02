using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class SetActivityAmbassadorHub: IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.Depth > 1)
                    return;

                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;
                if (target == null || target.LogicalName != "sam_activityambassador")
                    return;

                if (context.MessageName != "Create" && context.MessageName != "Update")
                    return;

                Entity preImage = null;
                if (context.MessageName == "Update" &&
                    context.PreEntityImages != null &&
                    context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                var ambassadorRef =
                    target.GetAttributeValue<EntityReference>("sam_ambassador") ??
                    preImage?.GetAttributeValue<EntityReference>("sam_ambassador");

                if (ambassadorRef == null)
                {
                    trace?.Trace("SetActivityAmbassadorDeliveryPartner: no sam_ambassador on record, nothing to do.");
                    return;
                }

                // Retrieve the contact to get sam_hub
                var contact = service.Retrieve(
                    "contact",
                    ambassadorRef.Id,
                    new ColumnSet("sam_deliverypartner"));

                var deliveryPartnerRef = contact.GetAttributeValue<EntityReference>("sam_deliverypartner");
                if (deliveryPartnerRef == null)
                {
                    trace?.Trace("SetActivityAmbassadorDeliveryPartner: contact {0} has no sam_hub set.", ambassadorRef.Id);
                    return;
                }

                // Update the Activity Ambassador with the hub
                var activityAmbassadorToUpdate = new Entity("sam_activityambassador");
                activityAmbassadorToUpdate.Id = context.PrimaryEntityId;

                activityAmbassadorToUpdate["sam_deliverypartner"] = deliveryPartnerRef;

                service.Update(activityAmbassadorToUpdate);

                trace?.Trace(
                    "SetActivityAmbassadorDeliveryPartner: set sam_deliverypartner on {0} to delivery partner {1}.",
                    context.PrimaryEntityId, deliveryPartnerRef.Id);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in SetActivityAmbassadorDeliveryPartner plugin: " + ex.Message, ex);
            }
        }
    }
}
