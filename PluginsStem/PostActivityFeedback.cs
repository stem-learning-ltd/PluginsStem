using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class PostActivityFeedback : IPlugin
    {

        private const int FEEDBACK_STATUS_COMPLETED = 999990002;
        private const int ACTIVITYAMB_STATUS_POSITION_FINISHED = 999990007;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));


            try
            {
                Entity entity = (Entity)context.InputParameters["Target"];
                var entityID = entity.Id;

                Entity feedback = null;

                if (context.PostEntityImages != null && context.PostEntityImages.Contains("PostImage"))
                {
                    feedback = (Entity)context.PostEntityImages["PostImage"];
                }
                else if (context.InputParameters.Contains("Target"))
                {
                    feedback = (Entity)context.InputParameters["Target"];
                }

                if (feedback == null)
                {
                    trace.Trace("PostActivityFeedback: No target/post image – exiting.");
                    return;
                }

                var statusOs = feedback.GetAttributeValue<OptionSetValue>("statuscode");
                if (statusOs == null || statusOs.Value != FEEDBACK_STATUS_COMPLETED)
                {
                    trace.Trace("PostActivityFeedback: Status not 'Completed' – exiting.");
                    return;
                }

                // Get participant (contact) and activity from feedback
                var contactRef = feedback.GetAttributeValue<EntityReference>("sam_contact");
                var activityRef = feedback.GetAttributeValue<EntityReference>("sam_activity");

                if (contactRef == null || activityRef == null)
                {
                    trace.Trace("PostActivityFeedback: Missing sam_contact or sam_activity on feedback – exiting.");
                    return;
                }

                // Find Activity Ambassador where:
                // sam_ambassador = contact AND sam_ambassadoractivity = activity
                var query = new QueryExpression("sam_activityambassador")
                {
                    ColumnSet = new ColumnSet("sam_activityambassadorid", "statuscode")
                };

                query.Criteria.AddCondition("sam_ambassador", ConditionOperator.Equal, contactRef.Id);
                query.Criteria.AddCondition("sam_ambassadoractivity", ConditionOperator.Equal, activityRef.Id);

                var ambassadors = service.RetrieveMultiple(query);

                if (ambassadors.Entities.Count == 0)
                {
                    trace.Trace("PostActivityFeedback: No matching Activity Ambassador found for contact {0} and activity {1}.",
                        contactRef.Id, activityRef.Id);
                    return;
                }

                // Update all matching Activity Ambassadors to "Position Finished"
                foreach (var amb in ambassadors.Entities)
                {
                    var activityAmbassadorToUpdate = new Entity("sam_activityambassador");
                    activityAmbassadorToUpdate.Id = amb.Id;


                    activityAmbassadorToUpdate["statuscode"] = new OptionSetValue(ACTIVITYAMB_STATUS_POSITION_FINISHED);
                    activityAmbassadorToUpdate["statecode"] = new OptionSetValue(1);


                    service.Update(activityAmbassadorToUpdate);
                    trace.Trace("PostActivityFeedback: Updated Activity Ambassador {0} to 'Position accepted'.", amb.Id);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in PostActivityFeedback plugin: " + ex.Message, ex);
            }

        }

    }
}
