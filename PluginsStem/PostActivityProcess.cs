using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;

namespace PluginsStem
{
    public class PostActivityProcess : IPlugin
    {
        #region Constants – entity & attribute logical names

        // Entities
        private const string ENTITY_ACTIVITY = "sam_ambassadoractivity";
        private const string ENTITY_ACTIVITYAMBASSADOR = "sam_activityambassador";
        private const string ENTITY_ACTIVITYFEEDBACK = "sam_activityfeedback";
        private const string ENTITY_NOTIFICATION = "sam_notification";
        private const string ENTITY_CONTACT = "contact";

        // Activity (sam_ambassadoractivity)
        private const string FIELD_ACTIVITY_ID = "activityid";
        private const string FIELD_ACTIVITY_SUBJECT = "subject";
        private const string FIELD_ACTIVITY_ENDDATE = "actualend";
        private const string FIELD_ACTIVITY_STATECODE = "statecode";
        private const string FIELD_ACTIVITY_STATUSCODE = "statuscode";
        private const string FIELD_ACTIVITY_OWNER = "sam_activityowner";
        private const string FIELD_ACTIVITY_NUMBER_CONFIRMED ="sam_numberofambassadorsconfirmed";

        // Activity Ambassador (sam_activityambassador)
        private const string FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP = "sam_ambassadoractivity";
        private const string FIELD_ACTIVITYAMB_CONTACT_LOOKUP = "sam_ambassador";
        private const string FIELD_ACTIVITYAMB_STATECODE = "statecode";
        private const string FIELD_ACTIVITYAMB_STATUSCODE = "statuscode";

        // Activity Feedback (sam_activityfeedback)
        private const string FIELD_FEEDBACK_CONTACT_LOOKUP = "sam_contact";
        private const string FIELD_FEEDBACK_ACTIVITY_LOOKUP = "sam_activity";
        private const string FIELD_FEEDBACK_STATECODE = "statecode";
        private const string FIELD_FEEDBACK_STATUSCODE = "statuscode";
        private const string FIELD_FEEDBACK_ACTIVITYAMB_LOOKUP = "sam_activityambassador";

        // Notification (sam_notification)
        private const string FIELD_NOTIFICATION_SUBJECT = "subject";
        private const string FIELD_NOTIFICATION_TO = "to";
        private const string FIELD_NOTIFICATION_DESCRIPTION = "description";
        private const string FIELD_NOTIFICATION_REGARDING = "regardingobjectid";

        // Contact
        private const string FIELD_CONTACT_LAST_ACTIVITY = "sam_lastactivitydate";

        #endregion

        #region Constants – option set values

        // Activity state/status
        private const int ACTIVITY_STATE_OPEN = 0;
        private const int ACTIVITY_STATE_COMPLETED = 1;

        private const int ACTIVITY_STATUS_COMPLETED = 2;
        private const int ACTIVITY_STATUS_DID_NOT_RUN = 999990001;

        // Activity Ambassador state/status
        private const int ACTIVITYAMB_STATE_ACTIVE = 0;
        private const int ACTIVITYAMB_STATE_INACTIVE = 1;

        private const int ACTIVITYAMB_STATUS_INTEREST_REGISTERED = 999990001;
        private const int ACTIVITYAMB_STATUS_POSITION_ACCEPTED = 999990002;
        private const int ACTIVITYAMB_STATUS_AWAITING_FEEDBACK = 999990003;
        private const int ACTIVITYAMB_STATUS_CLIENT_DECLINED_AMBASSADOR = 999990004;
        private const int ACTIVITYAMB_STATUS_AMBASSADOR_DECLINED_POSITION = 999990005;
        private const int ACTIVITYAMB_STATUS_ACTIVITY_SUGGESTED = 999990009;

        // Activity Feedback state/status
        private const int FEEDBACK_STATE_ACTIVE = 0;
        private const int FEEDBACK_STATUS_AWAITING = 1;

        #endregion

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracer = null;

            try
            {
                var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                var orgService = serviceFactory.CreateOrganizationService(context.UserId);
                tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                if (context.Depth > 1)
                    return;

                Process(orgService, tracer);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in PostActivityProcess Plugin: " + ex.Message, ex);
            }
        }

        /// Main process
        public void Process(IOrganizationService orgService, ITracingService tracer = null)
        {
            var activities = GetActivitiesToProcess(orgService);

            if (tracer != null)
                tracer.Trace("PostActivityProcess: Found {0} activities to process.", activities.Count);

            foreach (var activity in activities)
            {
                tracer?.Trace("Processing activity {0}", activity.Id);

                // =========================
                // confirmed rule FIRST
                // =========================
                //int? confirmed =
                //    activity.GetAttributeValue<int?>(FIELD_ACTIVITY_NUMBER_CONFIRMED);

                //if (!confirmed.HasValue || confirmed.Value == 0)
                //{
                //    tracer?.Trace("No confirmed ambassadors → Did Not Run");

                //    var request = new SetStateRequest
                //    {
                //        EntityMoniker = new EntityReference(ENTITY_ACTIVITY, activity.Id),
                //        State = new OptionSetValue(ACTIVITY_STATE_COMPLETED),
                //        Status = new OptionSetValue(ACTIVITY_STATUS_DID_NOT_RUN)
                //    };

                //    orgService.Execute(request);
                //    continue;
                //}

                var ambassadors = GetActivityAmbassadors(orgService, activity.Id);

                SyncScheduledFields(orgService, activity);
                SetActivityStatus(orgService, activity);

                var accepted = new List<Entity>();
                var interest = new List<Entity>();
                var suggested = new List<Entity>();

                foreach (var amb in ambassadors)
                {
                    var sc = amb.GetAttributeValue<OptionSetValue>(FIELD_ACTIVITYAMB_STATUSCODE)?.Value ?? -1;

                    switch (sc)
                    {
                        case ACTIVITYAMB_STATUS_POSITION_ACCEPTED:
                            accepted.Add(amb);
                            break;

                        case ACTIVITYAMB_STATUS_INTEREST_REGISTERED:
                            interest.Add(amb);
                            break;

                        case ACTIVITYAMB_STATUS_ACTIVITY_SUGGESTED:
                            suggested.Add(amb);
                            break;
                    }
                }

                UpdateAmbActivity(orgService, accepted, ACTIVITYAMB_STATE_ACTIVE, ACTIVITYAMB_STATUS_AWAITING_FEEDBACK);
                UpdateAmbActivity(orgService, interest, ACTIVITYAMB_STATE_INACTIVE, ACTIVITYAMB_STATUS_CLIENT_DECLINED_AMBASSADOR);
                UpdateAmbActivity(orgService, suggested, ACTIVITYAMB_STATE_INACTIVE, ACTIVITYAMB_STATUS_AMBASSADOR_DECLINED_POSITION);

                foreach (var amb in accepted)
                    RecalculateHoursForAmbassador(orgService, amb.Id);

                //foreach (var amb in interest)
                //    RecalculateHoursForAmbassador(orgService, amb.Id);

                //foreach (var amb in suggested)
                //    RecalculateHoursForAmbassador(orgService, amb.Id);

                foreach (var amb in accepted)
                {
                    var contactRef = amb.GetAttributeValue<EntityReference>(FIELD_ACTIVITYAMB_CONTACT_LOOKUP);
                    if (contactRef == null) continue;

                    var feedbackRef = CreateActivityFeedback(orgService, activity, amb, contactRef);

                    CreateNotification(
                        orgService,
                        contactRef,
                        feedbackRef,
                        $"Feedback requested for activity: {activity.GetAttributeValue<string>(FIELD_ACTIVITY_SUBJECT)}",
                        "Please provide feedback.");

                    UpdateContact(orgService, contactRef, activity.GetAttributeValue<DateTime?>(FIELD_ACTIVITY_ENDDATE) ?? DateTime.UtcNow);
                }

                CreateOwnerFeedback(orgService, activity, tracer);
            }
        }


        #region Helper methods

        private List<Entity> GetActivitiesToProcess(IOrganizationService orgService)
        {
            var query = new QueryExpression(ENTITY_ACTIVITY)
            {
                ColumnSet = new ColumnSet(
                    FIELD_ACTIVITY_ID,
                    FIELD_ACTIVITY_SUBJECT,
                    FIELD_ACTIVITY_ENDDATE,
                    FIELD_ACTIVITY_OWNER,
                    FIELD_ACTIVITY_NUMBER_CONFIRMED),

                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(FIELD_ACTIVITY_ENDDATE, ConditionOperator.LastXDays, 30),
                        new ConditionExpression(FIELD_ACTIVITY_STATECODE, ConditionOperator.Equal, ACTIVITY_STATE_OPEN)
                    }
                }
            };

            var results = orgService.RetrieveMultiple(query);
            return results.Entities.ToList();
        }

        public List<Entity> GetActivityAmbassadors(IOrganizationService orgService, Guid activityId)
        {
            try
            {
                var query = new QueryExpression(ENTITY_ACTIVITYAMBASSADOR)
                {
                    ColumnSet = new ColumnSet(
                        FIELD_ACTIVITYAMB_CONTACT_LOOKUP,
                        FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP,
                        FIELD_ACTIVITYAMB_STATECODE,
                        FIELD_ACTIVITYAMB_STATUSCODE,
                        "sam_activityhours_decimal")
                };

                query.Criteria.AddCondition(
                    FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP,
                    ConditionOperator.Equal,
                    activityId);

                var results = orgService.RetrieveMultiple(query);

                return results.Entities.ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in GetActivityAmbassadors function, Plugin: PostActivityProcess " + ex.Message,
                    ex);
            }
        }

        public void SetActivityStatus(IOrganizationService orgService, Entity activity)
        {
            int confirmed = activity.GetAttributeValue<int?>(FIELD_ACTIVITY_NUMBER_CONFIRMED) ?? 0;

            var request = new SetStateRequest
            {
                EntityMoniker = new EntityReference(
                    ENTITY_ACTIVITY,
                    activity.Id),

                State = new OptionSetValue(
                    ACTIVITY_STATE_COMPLETED),

                Status = new OptionSetValue(
                    confirmed > 0
                        ? ACTIVITY_STATUS_COMPLETED
                        : ACTIVITY_STATUS_DID_NOT_RUN)
            };

            orgService.Execute(request);
        }

        public void UpdateAmbActivity(
            IOrganizationService orgService,
            List<Entity> ambassadors,
            int newState,
            int newStatus)
        {
            foreach (var amb in ambassadors)
            {
                // state + status must be changed together
                var request = new SetStateRequest
                {
                    EntityMoniker = new EntityReference(
                        ENTITY_ACTIVITYAMBASSADOR,
                        amb.Id),

                    State = new OptionSetValue(newState),

                    Status = new OptionSetValue(newStatus)
                };

                orgService.Execute(request);
            }
        }

        // Ambassador feedback
        public EntityReference CreateActivityFeedback(
           IOrganizationService orgService,
           Entity activity,
           Entity ambassador,
           EntityReference contactRef)
        {
            if (contactRef == null)
                throw new InvalidPluginExecutionException("Contact reference is required.");

            var participantTypeValue = GetParticipantType(orgService, contactRef.Id);

            var feedback = new Entity(ENTITY_ACTIVITYFEEDBACK);

            feedback[FIELD_FEEDBACK_CONTACT_LOOKUP] = contactRef;
            feedback[FIELD_FEEDBACK_ACTIVITY_LOOKUP] = new EntityReference(ENTITY_ACTIVITY, activity.Id);
            feedback[FIELD_FEEDBACK_ACTIVITYAMB_LOOKUP] = new EntityReference(ENTITY_ACTIVITYAMBASSADOR, ambassador.Id);

            feedback[FIELD_FEEDBACK_STATECODE] = new OptionSetValue(FEEDBACK_STATE_ACTIVE);
            feedback[FIELD_FEEDBACK_STATUSCODE] = new OptionSetValue(FEEDBACK_STATUS_AWAITING);

            if (participantTypeValue.HasValue)
            {
                feedback["sam_participanttype"] =
                    new OptionSetValue(participantTypeValue.Value);
            }

            var id = orgService.Create(feedback);

            return new EntityReference(ENTITY_ACTIVITYFEEDBACK, id);
        }

        // Owner feedback
        public void CreateOwnerFeedback(
            IOrganizationService orgService,
            Entity activity,
            ITracingService tracer = null)
        {
            var ownerRef = activity.GetAttributeValue<EntityReference>(FIELD_ACTIVITY_OWNER);

            if (ownerRef == null || ownerRef.LogicalName != "contact")
                return;

            var participantTypeValue = GetParticipantType(orgService, ownerRef.Id);

            // Only Teacher / Youth Group Leader
            if (participantTypeValue != 2 &&
                participantTypeValue != 3)
            {
                return;
            }

            var feedback = new Entity(ENTITY_ACTIVITYFEEDBACK);

            feedback[FIELD_FEEDBACK_CONTACT_LOOKUP] = ownerRef;

            feedback[FIELD_FEEDBACK_ACTIVITY_LOOKUP] =
                new EntityReference(ENTITY_ACTIVITY, activity.Id);

            feedback[FIELD_FEEDBACK_STATECODE] =
                new OptionSetValue(FEEDBACK_STATE_ACTIVE);

            feedback[FIELD_FEEDBACK_STATUSCODE] =
                new OptionSetValue(FEEDBACK_STATUS_AWAITING);

            feedback["sam_participanttype"] =
                new OptionSetValue(participantTypeValue.Value);

            orgService.Create(feedback);

            if (tracer != null)
                tracer.Trace("Owner feedback created for activity {0}", activity.Id);
        }

        // stem_websiteaccounttype
        private int? GetParticipantType(IOrganizationService orgService, Guid contactId)
        {
            var contact = orgService.Retrieve(
                "contact",
                contactId,
                new ColumnSet("stem_websiteaccounttype"));

            var option = contact.GetAttributeValue<OptionSetValue>("stem_websiteaccounttype");

            if (option == null)
                return null;

            switch (option.Value)
            {
                case 157430000: // STEM Ambassador
                    return 1;

                case 157430003: // Teacher
                    return 2;

                case 157430001: // Youth Group Leader
                    return 3;

                default:
                    return null;
            }
        }

        public void CreateNotification(
            IOrganizationService orgService,
            EntityReference contactRef,
            EntityReference feedbackRef,
            string subject,
            string message)
        {
            var notification = new Entity(ENTITY_NOTIFICATION);

            notification[FIELD_NOTIFICATION_SUBJECT] = subject;
            notification[FIELD_NOTIFICATION_DESCRIPTION] = message;
            notification[FIELD_NOTIFICATION_REGARDING] = feedbackRef;

            var toParty = new Entity("activityparty");
            toParty["partyid"] = contactRef;

            var toCollection = new EntityCollection(new List<Entity> { toParty });

            notification[FIELD_NOTIFICATION_TO] = toCollection;

            orgService.Create(notification);
        }

        public void UpdateContact(
            IOrganizationService orgService,
            EntityReference contactRef,
            DateTime lastActivityDate)
        {
            var update = new Entity(ENTITY_CONTACT)
            {
                Id = contactRef.Id
            };

            update[FIELD_CONTACT_LAST_ACTIVITY] = lastActivityDate;

            orgService.Update(update);
        }

        private void SyncScheduledFields(IOrganizationService service, Entity activity)
        {
            var fullActivity = service.Retrieve(
                ENTITY_ACTIVITY,
                activity.Id,
                new ColumnSet(
                    "actualstart",
                    "actualend",
                    "scheduledstart",
                    "scheduledend"));

            var actualStart =
                fullActivity.GetAttributeValue<DateTime?>("actualstart");

            var actualEnd =
                fullActivity.GetAttributeValue<DateTime?>("actualend");

            var scheduledStart =
                fullActivity.GetAttributeValue<DateTime?>("scheduledstart");

            var scheduledEnd =
                fullActivity.GetAttributeValue<DateTime?>("scheduledend");

            var update = new Entity(ENTITY_ACTIVITY)
            {
                Id = activity.Id
            };

            bool needsUpdate = false;

            if (actualStart != null && scheduledStart == null)
            {
                update["scheduledstart"] = actualStart;
                needsUpdate = true;
            }

            if (actualEnd != null && scheduledEnd == null)
            {
                update["scheduledend"] = actualEnd;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                service.Update(update);
            }
        }


        private void RecalculateHoursForAmbassador(IOrganizationService service, Guid ambassadorId)
        {
            var ambassador = service.Retrieve(
                ENTITY_ACTIVITYAMBASSADOR,
                ambassadorId,
                new ColumnSet(
                    FIELD_ACTIVITYAMB_CONTACT_LOOKUP,
                    FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP,
                    FIELD_ACTIVITYAMB_STATUSCODE));

            var status =ambassador.GetAttributeValue<OptionSetValue>(FIELD_ACTIVITYAMB_STATUSCODE)?.Value ?? -1;

            if (status != ACTIVITYAMB_STATUS_AWAITING_FEEDBACK &&
                status != 999990007)
            {
                var clear = new Entity(ENTITY_ACTIVITYAMBASSADOR)
                {
                    Id = ambassadorId
                };

                clear["sam_activityhours_decimal"] = null;

                service.Update(clear);

                return;
            }

            var contactRef =
                ambassador.GetAttributeValue<EntityReference>(
                    FIELD_ACTIVITYAMB_CONTACT_LOOKUP);

            var activityRef =
                ambassador.GetAttributeValue<EntityReference>(
                    FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP);

            if (contactRef == null || activityRef == null)
                return;

            // =====================================
            // Activity duration
            // =====================================

            var activity = service.Retrieve(
                ENTITY_ACTIVITY,
                activityRef.Id,
                new ColumnSet("actualdurationminutes"));

            int minutes =
                activity.GetAttributeValue<int?>("actualdurationminutes") ?? 0;

            decimal hours = ConvertMinutesToQuarterHours(minutes);

            // =====================================
            // Update ambassador hours
            // =====================================

            var updateAmbassador = new Entity(ENTITY_ACTIVITYAMBASSADOR)
            {
                Id = ambassadorId
            };

            updateAmbassador["sam_activityhours_decimal"] = hours;

            service.Update(updateAmbassador);

            // =====================================
            // Recalculate contact total
            // =====================================

            decimal totalHours = 0;

            var query = new QueryExpression(ENTITY_ACTIVITYAMBASSADOR)
            {
                ColumnSet = new ColumnSet(
                    FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP,
                    FIELD_ACTIVITYAMB_STATUSCODE)
            };

            query.Criteria.AddCondition(
                FIELD_ACTIVITYAMB_CONTACT_LOOKUP,
                ConditionOperator.Equal,
                contactRef.Id);

            var statusFilter =
                new FilterExpression(LogicalOperator.Or);

            statusFilter.AddCondition(
                FIELD_ACTIVITYAMB_STATUSCODE,
                ConditionOperator.Equal,
                ACTIVITYAMB_STATUS_AWAITING_FEEDBACK);

            statusFilter.AddCondition(
                FIELD_ACTIVITYAMB_STATUSCODE,
                ConditionOperator.Equal,
                999990007);

            query.Criteria.AddFilter(statusFilter);

            var allAmbassadors =
                service.RetrieveMultiple(query).Entities;

            foreach (var amb in allAmbassadors)
            {
                var actRef =
                    amb.GetAttributeValue<EntityReference>(
                        FIELD_ACTIVITYAMB_ACTIVITY_LOOKUP);

                if (actRef == null)
                    continue;

                var act = service.Retrieve(
                    ENTITY_ACTIVITY,
                    actRef.Id,
                    new ColumnSet("actualdurationminutes"));

                int mins =
                    act.GetAttributeValue<int?>("actualdurationminutes") ?? 0;

                totalHours += ConvertMinutesToQuarterHours(mins);
            }

            // =====================================
            // Update contact total
            // =====================================

            var updateContact = new Entity(ENTITY_CONTACT)
            {
                Id = contactRef.Id
            };

            updateContact["sam_ambassadoractivityhours_decimal"] = totalHours;

            service.Update(updateContact);
        }

        private decimal ConvertMinutesToQuarterHours(int minutes)
        {
            if (minutes <= 0)
                return 0m;

            decimal hours = minutes / 60m;

            return Math.Round(hours * 4, 0) / 4m;
        }

        #endregion
    }
}