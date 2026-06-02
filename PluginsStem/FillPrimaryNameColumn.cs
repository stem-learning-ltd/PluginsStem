using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PluginsStem
{
    public class FillPrimaryNameColumn : IPlugin
    {
        public void Execute( IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(context.UserId);
                ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                Entity entity = (Entity)context.InputParameters["Target"];



                if (entity.LogicalName == "amb_user")
                {
                    var ambassadorUserToUpdate = new Entity("amb_user");
                    ambassadorUserToUpdate.Id = entity.Id;

                    var ambassadorFirstNameUser = entity.GetAttributeValue<string>("amb_firstname").ToString();
                    var ambassadorLastNameUser = entity.GetAttributeValue<string>("amb_lastname").ToString();
                    var fullNameAmbassadorUser = ambassadorFirstNameUser + ambassadorLastNameUser;

                    ambassadorUserToUpdate["amb_name"] = fullNameAmbassadorUser;

                    orgService.Update(ambassadorUserToUpdate);
                }

                if (entity.LogicalName == "amb_teacher")
                {
                    if (entity.GetAttributeValue<EntityReference>("amb_userid") != null && entity.Contains("amb_userid"))
                    {
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var teacherRef = entity.Id;

                        var ambassadorUser = GetAmbassadorUser(orgService, userId);
                        var ambassadorFirstNameTeacher = ambassadorUser["amb_firstname"].ToString();
                        var ambassadorLastNameTeacher = ambassadorUser["amb_lastname"].ToString();

                        var fullNameTeacher = ambassadorFirstNameTeacher + ambassadorLastNameTeacher;

                        var teacherToUpdate = new Entity("amb_teacher");
                        teacherToUpdate.Id = teacherRef;

                        teacherToUpdate["name"] = fullNameTeacher;
                        orgService.Update(teacherToUpdate);

                    }
                    else
                    {
                        return;
                    }
                }

                if (entity.LogicalName == "amb_youthgroupleader")
                {
                    if (entity.GetAttributeValue<EntityReference>("amb_userid") != null && entity.Contains("amb_userid"))
                    {
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var youthGroupLeaderRef = entity.Id;

                        var ambassadorUser = GetAmbassadorUser(orgService, userId);
                        var ambassadorFirstNameYouthGroupLeader = ambassadorUser["amb_firstname"].ToString();
                        var ambassadorLastNameYouthGroupLeader = ambassadorUser["amb_lastname"].ToString();

                        var fullNameYouthGroupLeader = ambassadorFirstNameYouthGroupLeader + ambassadorLastNameYouthGroupLeader;

                        var youthGroupLeaderToUpdate = new Entity("amb_youthgroupleader");
                        youthGroupLeaderToUpdate.Id = youthGroupLeaderRef;

                        youthGroupLeaderToUpdate["name"] = fullNameYouthGroupLeader;
                        orgService.Update(youthGroupLeaderToUpdate);

                    }
                    else
                    {
                        return;
                    }
                }

                if (entity.LogicalName == "amb_partneruser")
                {
                    if (entity.GetAttributeValue<EntityReference>("amb_userid") != null && entity.Contains("amb_userid"))
                    {
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var partnerUserRef = entity.Id;

                        var ambassadorUser = GetAmbassadorUser(orgService, userId);
                        var ambassadorFirstNamePartnerUser = ambassadorUser["amb_firstname"].ToString();
                        var ambassadorLastNamePartnerUser = ambassadorUser["amb_lastname"].ToString();

                        var fullNamePartnerUser = ambassadorFirstNamePartnerUser + ambassadorLastNamePartnerUser;

                        var partnerUserToUpdate = new Entity("amb_partneruser");
                        partnerUserToUpdate.Id = partnerUserRef;

                        partnerUserToUpdate["name"] = partnerUserToUpdate;
                        orgService.Update(partnerUserToUpdate);

                    }
                    else
                    {
                        return;
                    }
                }

                if (entity.LogicalName == "amb_ambassadorscheme")
                {
                    if ((entity.GetAttributeValue<EntityReference>("amb_ambassador") != null && entity.Contains("amb_ambassador")) || (entity.GetAttributeValue<EntityReference>("amb_schemeid") != null && entity.Contains("amb_schemeid")))
                    {
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var ambassadorSchemeRef = entity.Id;

                        var ambassadorUser = GetAmbassadorUserName(orgService, userId);
                        var ambassadorFullNameAmbassadorScheme = ambassadorUser["amb_name"].ToString();

                        var ambassadorSchemeEntity = orgService.Retrieve("amb_ambassadorscheme", ambassadorSchemeRef, new ColumnSet("amb_name", "amb_joinedschemeon"));

                        var fullNameAmbassadorScheme = ambassadorSchemeEntity["amb_name"] + ambassadorFullNameAmbassadorScheme;
                        
                        var ambassadorSchemeToUpdate = new Entity("amb_ambassadorscheme");
                        ambassadorSchemeToUpdate.Id = ambassadorSchemeRef;

                        ambassadorSchemeToUpdate["name"] = fullNameAmbassadorScheme;

                        if (!ambassadorSchemeEntity.GetAttributeValue<DateTime?>("amb_joinedschemeon").HasValue)

                        {
                            ambassadorSchemeToUpdate["amb_joinedschemeon"] = DateTime.UtcNow;
                            orgService.Update(ambassadorSchemeToUpdate);

                        }
                        else
                        {
                            orgService.Update(ambassadorSchemeToUpdate);

                        }
                    }
                    else
                    {
                        return;
                    }
                }

                if (entity.LogicalName == "amb_ambassador")
                {
                    if (entity.GetAttributeValue<EntityReference>("amb_userid") != null && entity.Contains("amb_userid"))
                    {
                        var ambassadorRef = entity.Id;
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;

                        var ambassadorUserEntity = GetAmbassadorUser(orgService, userId);

                        var ambassadorFirstNameAmbassador = ambassadorUserEntity["amb_firstname"].ToString();
                        var ambassadorLastNameAmbassador = ambassadorUserEntity["amb_lastname"].ToString();

                        var fullNameAmbassador = ambassadorFirstNameAmbassador + ambassadorLastNameAmbassador;

                        var ambassadorToUpdate = new Entity("amb_ambassador");
                        ambassadorToUpdate.Id = ambassadorRef;

                        ambassadorToUpdate["amb_name"] = fullNameAmbassador;
                        orgService.Update(ambassadorToUpdate);

                        //var ambassadorUserToUpdate = new Entity("amb_user");
                        //ambassadorUserToUpdate.Id = userId;
                        //
                        //ambassadorUserToUpdate["amb_userid"] = userId;
                        //orgService.Update(ambassadorUserToUpdate);

                    }
                    else
                    {
                        return;
                    }
                        
                }

                if (entity.LogicalName == "amb_activityscheme")
                {
                    if ((entity.GetAttributeValue<EntityReference>("amb_ambassador") != null && entity.Contains("amb_ambassador")) || (entity.GetAttributeValue<EntityReference>("amb_schemeid") != null && entity.Contains("amb_schemeid")))
                    {
                        var userId = entity.GetAttributeValue<EntityReference>("amb_userid").Id;
                        var ambassadorSchemeRef = entity.Id;

                        var ambassadorUser = GetAmbassadorUserName(orgService, userId);
                        var ambassadorFullNameAmbassadorScheme = ambassadorUser["amb_name"].ToString();

                        var ambassadorSchemeEntity = orgService.Retrieve("amb_ambassadorscheme", ambassadorSchemeRef, new ColumnSet("amb_name", "amb_joinedschemeon"));

                        var fullNameAmbassadorScheme = ambassadorSchemeEntity["amb_name"] + ambassadorFullNameAmbassadorScheme;

                        var ambassadorSchemeToUpdate = new Entity("amb_ambassadorscheme");
                        ambassadorSchemeToUpdate.Id = ambassadorSchemeRef;

                        ambassadorSchemeToUpdate["name"] = fullNameAmbassadorScheme;

                        if (!ambassadorSchemeEntity.GetAttributeValue<DateTime?>("amb_joinedschemeon").HasValue)

                        {
                            ambassadorSchemeToUpdate["amb_joinedschemeon"] = DateTime.UtcNow;
                            orgService.Update(ambassadorSchemeToUpdate);

                        }
                        else
                        {
                            orgService.Update(ambassadorSchemeToUpdate);

                        }
                    }
                    else
                    {
                        return;
                    }
                }
                

            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException("An error has occurred: " + e.Message);
            }
        }

        public Entity GetAmbassadorUser(IOrganizationService orgService, Guid ID)
        {
            var ambassadorUserEntity = orgService.Retrieve("amb_user", ID, new ColumnSet("amb_firstname", "amb_lastname"));
            return ambassadorUserEntity;
        }

        public Entity GetAmbassadorUserName(IOrganizationService orgService, Guid ID)
        {
            var ambassadorUserEntity = orgService.Retrieve("amb_user", ID, new ColumnSet("amb_name"));
            return ambassadorUserEntity;
        }

        public List<Entity> GetAmbassadorUsers(IOrganizationService orgService, Guid ID)
        {          
            List<Entity> ambassadorUsersList = new List<Entity>();

            try
            {
                var query = new QueryExpression("amb_teacher");
                query.Criteria.AddCondition("amb_teacherid", ConditionOperator.Equal, ID);

                var u = query.AddLink("amb_user", "amb_userid", "amb_userid");
                u.EntityAlias = "u";
                u.Columns.AddColumns("amb_firstname", "amb_lastname");


                EntityCollection resultsTemp;

                do
                {
                    resultsTemp = orgService.RetrieveMultiple(query);

                    foreach (var record in resultsTemp.Entities)
                    {
                        ambassadorUsersList.Add(record);
                    }

                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = resultsTemp.PagingCookie;
                }
                while (resultsTemp.MoreRecords);
            }
            catch (Exception ex)
            {
                
            }


            return ambassadorUsersList;
        }

    }
}

