using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class ExpireActivityFeedback : IPlugin
    {
        private const string ActivityFeedbackEntity = "sam_activityfeedback";
        private const string ActivityAmbassadorEntity = "sam_activityambassador";

        private const int FeedbackState_Active = 0;
        private const int FeedbackStatus_Awaiting = 1;
        private const int FeedbackStatus_Expired = 2;

        private const int AmbassadorStatus_PositionFinished = 999990007;

        private const int ExpiryDays = 60;

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

                Process(service, trace);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in ExpireActivityFeedback plugin: " + ex.Message, ex);
            }
        }

        private void Process(IOrganizationService orgService, ITracingService trace)
        {
            var feedbacksToExpire = GetActivityFeedbacksToExpire(orgService);

            if (!feedbacksToExpire.Any())
                return;

            foreach (var fb in feedbacksToExpire)
            {
                var fbId = fb.Id;

                //var feedbackToUpdate = new Entity(ActivityFeedbackEntity);
                //feedbackToUpdate.Id = fbId;

                //feedbackToUpdate["statuscode"] = new OptionSetValue(1);
                //feedbackToUpdate["statuscode"] = new OptionSetValue(FeedbackStatus_Expired);

                //orgService.Update(feedbackToUpdate);

                var requestActivityFeedback = new SetStateRequest
                {
                    EntityMoniker = new EntityReference("sam_activityfeedback", fb.Id),
                    State = new OptionSetValue(1),
                    Status = new OptionSetValue(FeedbackStatus_Expired)
                };

                orgService.Execute(requestActivityFeedback);


                var ambRef = fb.GetAttributeValue<EntityReference>("sam_activityambassador");
                if (ambRef != null)
                {
                    //var activityAmbassadorToUpdate = new Entity(ActivityAmbassadorEntity);
                    //activityAmbassadorToUpdate.Id = ambRef.Id;

                    var requestActivityAmbassador = new SetStateRequest
                    {
                        EntityMoniker = new EntityReference("sam_activityambassador", ambRef.Id),
                        State = new OptionSetValue(1),
                        Status = new OptionSetValue(AmbassadorStatus_PositionFinished)
                    };

                    orgService.Execute(requestActivityAmbassador);

                    //activityAmbassadorToUpdate["statuscode"] = new OptionSetValue(AmbassadorStatus_PositionFinished);
                    //orgService.Update(activityAmbassadorToUpdate);
                }
            }
        }

        /// Returns activity feedback records older than X days that are still Awaiting.
        private List<Entity> GetActivityFeedbacksToExpire(IOrganizationService orgService)
        {
            var results = new EntityCollection();
            var resultsTemp = new EntityCollection();

            try
            {
                var query_createdon = ExpiryDays;
                var query_statuscode = FeedbackStatus_Awaiting;
                var query_statecode = FeedbackState_Active;

                var query = new QueryExpression(ActivityFeedbackEntity)
                {
                    ColumnSet = new ColumnSet(
                        "sam_activityfeedbackid",
                        "statuscode",
                        "statecode",
                        "sam_activityambassador"
                    )
                };

                query.Criteria.AddCondition("createdon", ConditionOperator.OlderThanXDays, query_createdon);
                query.Criteria.AddCondition("statuscode", ConditionOperator.Equal, query_statuscode);
                query.Criteria.AddCondition("statecode", ConditionOperator.Equal, query_statecode);

                query.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = null
                };

                do
                {
                    resultsTemp = orgService.RetrieveMultiple(query);
                    results.Entities.AddRange(resultsTemp.Entities);
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = resultsTemp.PagingCookie;
                }
                while (resultsTemp.MoreRecords);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error in GetActivityFeedbacksToExpire, plugin ExpireActivityFeedback: " + ex.Message, ex);
            }

            return results.Entities.ToList();
        }
    }
}
