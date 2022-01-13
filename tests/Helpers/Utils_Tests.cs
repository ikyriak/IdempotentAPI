using System.Collections.Generic;
using FluentAssertions;
using IdempotentAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace IdempotentAPI.Tests.Helpers
{
    public class Utils_Tests
    {
        [Fact]
        public void DederializeSerializedData_ShoultResultToTheOriginalData()
        {
            // Arrange
            Dictionary<string, object> cacheData = new Dictionary<string, object>();

            // Cache string, int, etc.
            cacheData.Add("Request.Method", "POST");
            cacheData.Add("Response.StatusCode", 200);

            // Cache a Dictionary containing a List
            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>();
            headers.Add("myHeader1", new List<string>() { "value1-1", "value1-2" });
            headers.Add("myHeader2", new List<string>() { "value2-1", "value2-1" });
            cacheData.Add("Response.Headers", headers);

            // Cache a Dictionary containing an object
            Dictionary<string, object> resultObjects = new Dictionary<string, object>();
            CreatedAtRouteResult createdAtRouteResult = new CreatedAtRouteResult("myRoute", new { id = 1 }, new { prop1 = 1, prop2 = "2" });
            resultObjects.Add("ResultType", "ResultType");
            resultObjects.Add("ResultValue", createdAtRouteResult.Value);

            // Cache a Dictionary containing string
            Dictionary<string, string> routeValues = new Dictionary<string, string>();
            routeValues.Add("route1", "routeValue1");
            routeValues.Add("route2", "routeValue2");
            resultObjects.Add("ResultRouteValues", routeValues);

            cacheData.Add("Context.Result", resultObjects);


            // Act

            // Step 1. Serialize data:
            byte[] serializedData = cacheData.Serialize();

            // Step 1. Deserialize the serialized data:
            Dictionary<string, object> cacheDataAfterSerialization =
                serializedData.DeSerialize<Dictionary<string, object>>();


            // Assert
            cacheDataAfterSerialization.Should().BeEquivalentTo(cacheData);
        }
    }
}
