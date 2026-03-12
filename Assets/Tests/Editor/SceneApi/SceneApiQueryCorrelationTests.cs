using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.Tests.Editor.SceneApi
{
    public class SceneApiQueryCorrelationTests
    {
        [Test]
        public void AddRequestId_AddsCorrelationFieldToQueryResponse()
        {
            var response = JObject.FromObject(new
            {
                type = "scene.query_response",
                method = "scene.query_summary"
            });

            var correlated = SceneApiManager.AddRequestId(response, "qry_1");

            Assert.AreEqual("qry_1", correlated.Value<string>("request_id"));
            Assert.AreEqual("scene.query_response", correlated.Value<string>("type"));
        }

        [Test]
        public void AddRequestId_SkipsEmptyRequestId()
        {
            var response = JObject.FromObject(new
            {
                type = "scene.query_response",
                method = "scene.query_summary"
            });

            var correlated = SceneApiManager.AddRequestId(response, string.Empty);

            Assert.IsNull(correlated["request_id"]);
        }
    }
}
