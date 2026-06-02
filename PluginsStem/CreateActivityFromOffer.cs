using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace PluginsStem
{
    public class CreateActivityFromOffer : IPlugin
    {
        private const string SAM_OFFER_LOGICAL_NAME = "sam_offer";
        private const string SAM_ACTIVITY_LOGICAL_NAME = "sam_ambassadoractivity";
        private const string ACTIVITY_NAME_COL = "subject";
        private const string ACTIVITY_DESC_COL = "description";
        private const string ACTIVITY_TYPE_COL = "sam_activitytype";
        private const string ACTIVITY_LOOKUP_ON_ACTIVITY = "sam_offer";
        private const string AREAS_OF_EXPERTISE_COL = "sam_areasofexpertise";

        // Multi-choice mappings
        private const string OFFER_AUDIENCE_AGE_RANGE = "sam_audienceagerange";
        private const string ACTIVITY_AUDIENCE_AGE_RANGES = "sam_audienceageranges";

        private const string OFFER_AUDIENCE_TYPE = "sam_typeofaudience";
        private const string ACTIVITY_AUDIENCE_TYPES = "sam_audiencetypes";

        // Activity Ambassador
        private const string ACTIVITYAMB_ENTITY = "sam_activityambassador";
        private const string ACTIVITYAMB_ACTIVITY_LOOKUP = "sam_ambassadoractivity";
        private const string ACTIVITYAMB_CONTACT_LOOKUP = "sam_ambassador";

        private const int ACTIVITYAMB_STATE_ACTIVE = 0;
        private const int ACTIVITYAMB_STATUS_POSITION_ACCEPTED = 999990002;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                var recordIdStr = context.InputParameters["recordID"] as string;
                var name = context.InputParameters["name"] as string ?? string.Empty;
                var description = context.InputParameters["description"] as string ?? string.Empty;

                int activityType = 0;
                if (context.InputParameters.Contains("activityType") && context.InputParameters["activityType"] != null)
                    activityType = Convert.ToInt32(context.InputParameters["activityType"]);

                var connectToJson = context.InputParameters.Contains("connectToIDs")
                    ? context.InputParameters["connectToIDs"] as string
                    : "[]";

                var offerOwnerIdStr = context.InputParameters.Contains("offerOwnerID")
                    ? context.InputParameters["offerOwnerID"] as string
                    : null;

                var areasString = context.InputParameters.Contains("areasOfExpertise")
                    ? context.InputParameters["areasOfExpertise"] as string
                    : null;

                if (string.IsNullOrWhiteSpace(recordIdStr))
                    throw new InvalidPluginExecutionException("recordID is required.");

                var offerId = Guid.Parse(recordIdStr);
                var connectToIds = JsonConvert.DeserializeObject<List<Guid>>(connectToJson) ?? new List<Guid>();

                // Retrieve fields
                var offer = service.Retrieve(
                    SAM_OFFER_LOGICAL_NAME,
                    offerId,
                    new ColumnSet(
                        "sam_offerowner",
                        AREAS_OF_EXPERTISE_COL,
                        OFFER_AUDIENCE_AGE_RANGE,
                        OFFER_AUDIENCE_TYPE
                    ));

                // =========================
                // Areas of Expertise
                // =========================
                OptionSetValueCollection areasCollection = null;

                if (!string.IsNullOrWhiteSpace(areasString))
                {
                    var parts = areasString
                        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    var list = new List<OptionSetValue>();
                    foreach (var p in parts)
                    {
                        if (int.TryParse(p, out var val))
                        {
                            list.Add(new OptionSetValue(val));
                        }
                    }

                    if (list.Count > 0)
                        areasCollection = new OptionSetValueCollection(list);
                }
                else
                {
                    areasCollection = offer.GetAttributeValue<OptionSetValueCollection>(AREAS_OF_EXPERTISE_COL);
                }

                // =========================
                // Multi-choice mappings
                // =========================

                var audienceAgeRanges = ConvertToMultiSelect(
                    offer,
                    OFFER_AUDIENCE_AGE_RANGE);

                var audienceTypes = ConvertToMultiSelect(
                    offer,
                    OFFER_AUDIENCE_TYPE);

                // =========================
                // Create Activity
                // =========================

                var activity = new Entity(SAM_ACTIVITY_LOGICAL_NAME);
                activity[ACTIVITY_NAME_COL] = name;
                activity[ACTIVITY_DESC_COL] = description;
                activity[ACTIVITY_TYPE_COL] = new OptionSetValue(activityType);
                activity[ACTIVITY_LOOKUP_ON_ACTIVITY] = new EntityReference(SAM_OFFER_LOGICAL_NAME, offerId);
                
                // =========================
                // Multi-choices
                // =========================

                if (areasCollection != null && areasCollection.Count > 0)
                    activity[AREAS_OF_EXPERTISE_COL] = areasCollection;

                if (audienceAgeRanges != null && audienceAgeRanges.Count > 0)
                    activity[ACTIVITY_AUDIENCE_AGE_RANGES] = audienceAgeRanges;

                if (audienceTypes != null && audienceTypes.Count > 0)
                    activity[ACTIVITY_AUDIENCE_TYPES] = audienceTypes;

                var activityId = service.Create(activity);

                // =========================
                // Connections
                // =========================

                if (connectToIds.Any())
                {
                    foreach (var accountId in connectToIds)
                    {
                        var connection = new Entity("connection");
                        connection["record1id"] = new EntityReference(SAM_ACTIVITY_LOGICAL_NAME, activityId);
                        connection["record2id"] = new EntityReference("account", accountId);
                        connection["sam_type"] = new OptionSetValue(3);
                        connection["effectivestart"] = DateTime.UtcNow;
                        connection["description"] = $"Created with activity {activityId}";
                        service.Create(connection);
                    }
                }

                EntityReference ownerRef = null;

                if (!string.IsNullOrWhiteSpace(offerOwnerIdStr) &&
                    Guid.TryParse(offerOwnerIdStr, out Guid ownerGuidFromParam))
                {
                    ownerRef = new EntityReference("contact", ownerGuidFromParam);
                }
                else
                {
                    ownerRef = offer.GetAttributeValue<EntityReference>("sam_offerowner");
                }

                if (ownerRef != null)
                {
                    var activityAmbassador = new Entity(ACTIVITYAMB_ENTITY);
                    activityAmbassador[ACTIVITYAMB_ACTIVITY_LOOKUP] =
                        new EntityReference(SAM_ACTIVITY_LOGICAL_NAME, activityId);
                    activityAmbassador[ACTIVITYAMB_CONTACT_LOOKUP] = ownerRef;

                    activityAmbassador["statecode"] = new OptionSetValue(ACTIVITYAMB_STATE_ACTIVE);
                    activityAmbassador["statuscode"] =
                        new OptionSetValue(ACTIVITYAMB_STATUS_POSITION_ACCEPTED);

                    service.Create(activityAmbassador);
                }
                else
                {
                    trace?.Trace("No offer owner found; ActivityAmbassador not created.");
                }

                context.OutputParameters["activityID"] = activityId.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException($"CreateActivityFromOffer failed: {ex.Message}", ex);
            }
        }

        // handles BOTH single choice + multi-select
        private OptionSetValueCollection ConvertToMultiSelect(Entity entity, string attributeName)
        {
            if (!entity.Attributes.Contains(attributeName))
                return null;

            var value = entity[attributeName];

            // Already multi-select
            if (value is OptionSetValueCollection multi)
                return multi;

            // Old single select → convert
            if (value is OptionSetValue single)
                return new OptionSetValueCollection
                {
                    new OptionSetValue(single.Value)
                };

            return null;
        }
    }
}