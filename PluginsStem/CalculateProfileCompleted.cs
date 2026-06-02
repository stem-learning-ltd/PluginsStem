using System;
using Microsoft.Xrm.Sdk;

namespace PluginsStem
{
    public class CalculateProfileCompleted : IPlugin
    {
        // Required profile fields
        private const string FIELD_DISTANCE_OF_OPERATION = "sam_distanceofoperation";
        private const string FIELD_DATE_OF_BIRTH = "sam_dateofbirth";
        private const string FIELD_ETHNICITY = "sam_ethnicity";
        private const string FIELD_GENDER = "gendercode";
        private const string FIELD_POSTCODE = "address1_postalcode";
        private const string FIELD_POSTCODE_OF_OPERATION = "sam_postcodeofoperation";
        private const string FIELD_PHONE = "telephone1";
        private const string FIELD_EMPLOYMENT_STATUS = "sam_employmentstatus";

        // Result field
        private const string FIELD_PROFILE_COMPLETED = "sam_profilecompleted";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            var factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var service =
                factory.CreateOrganizationService(context.UserId);

            var trace =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (context.Depth > 1)
                    return;

                // Only Create / Update
                if (context.MessageName != "Create" &&
                    context.MessageName != "Update")
                {
                    return;
                }

                if (!context.InputParameters.Contains("Target"))
                    return;

                var target = context.InputParameters["Target"] as Entity;

                if (target == null || target.LogicalName != "contact")
                    return;

                // =========================
                // PRE IMAGE
                // =========================

                Entity preImage = null;

                if (context.PreEntityImages != null &&
                    context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }

                // =========================
                // Validate required fields
                // =========================

                bool hasDistanceOfOperation =
                    GetDecimalValue(target, preImage, FIELD_DISTANCE_OF_OPERATION).HasValue;

                bool hasDateOfBirth =
                    GetDateValue(target, preImage, FIELD_DATE_OF_BIRTH).HasValue;

                bool hasEthnicity =
                    GetOptionSetValue(target, preImage, FIELD_ETHNICITY).HasValue;

                bool hasGender =
                    GetOptionSetValue(target, preImage, FIELD_GENDER).HasValue;

                bool hasPostcode =
                    !string.IsNullOrWhiteSpace(
                        GetStringValue(target, preImage, FIELD_POSTCODE));

                bool hasPostcodeOfOperation =
                    !string.IsNullOrWhiteSpace(
                        GetStringValue(target, preImage, FIELD_POSTCODE_OF_OPERATION));

                bool hasPhone =
                    !string.IsNullOrWhiteSpace(
                        GetStringValue(target, preImage, FIELD_PHONE));

                bool hasEmploymentStatus =
                    GetOptionSetValue(target, preImage, FIELD_EMPLOYMENT_STATUS).HasValue;

                trace?.Trace(
                    "CalculateProfileCompleted: distance={0}, dob={1}, ethnicity={2}, gender={3}, postcode={4}, operationPostcode={5}, phone={6}, employment={7}",
                    hasDistanceOfOperation,
                    hasDateOfBirth,
                    hasEthnicity,
                    hasGender,
                    hasPostcode,
                    hasPostcodeOfOperation,
                    hasPhone,
                    hasEmploymentStatus);

                // =========================
                // Final result
                // =========================

                bool profileCompleted =
                    hasDistanceOfOperation &&
                    hasDateOfBirth &&
                    hasEthnicity &&
                    hasGender &&
                    hasPostcode &&
                    hasPostcodeOfOperation &&
                    hasPhone &&
                    hasEmploymentStatus;

                trace?.Trace(
                    "CalculateProfileCompleted: calculated profileCompleted={0}",
                    profileCompleted);

                // =========================
                // Explicit update
                // =========================

                var update = new Entity("contact")
                {
                    Id = target.Id
                };

                update[FIELD_PROFILE_COMPLETED] = profileCompleted;

                service.Update(update);

                trace?.Trace(
                    "CalculateProfileCompleted: sam_profilecompleted updated successfully.");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "CalculateProfileCompleted failed: " + ex.Message,
                    ex);
            }
        }

        // =========================
        // Helpers
        // =========================

        private string GetStringValue(
            Entity target,
            Entity preImage,
            string fieldName)
        {
            if (target.Contains(fieldName))
                return target.GetAttributeValue<string>(fieldName);

            return preImage?.GetAttributeValue<string>(fieldName);
        }

        private DateTime? GetDateValue(
            Entity target,
            Entity preImage,
            string fieldName)
        {
            if (target.Contains(fieldName))
                return target.GetAttributeValue<DateTime?>(fieldName);

            return preImage?.GetAttributeValue<DateTime?>(fieldName);
        }

        private int? GetOptionSetValue(
            Entity target,
            Entity preImage,
            string fieldName)
        {
            if (target.Contains(fieldName))
            {
                var targetValue =
                    target.GetAttributeValue<OptionSetValue>(fieldName);

                return targetValue?.Value;
            }

            var preValue =
                preImage?.GetAttributeValue<OptionSetValue>(fieldName);

            return preValue?.Value;
        }

        private decimal? GetDecimalValue(
            Entity target,
            Entity preImage,
            string fieldName)
        {
            if (target.Contains(fieldName))
                return target.GetAttributeValue<decimal?>(fieldName);

            return preImage?.GetAttributeValue<decimal?>(fieldName);
        }
    }
}