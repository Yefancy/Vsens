using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;

namespace VsensAgent.Tests.Editor.Network
{
    public class WsClientTests
    {
        private static MethodInfo ResolveHandleMessage()
        {
            var method = typeof(WsClient).GetMethod("HandleMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected WsClient.HandleMessage to exist.");
            return method;
        }

        private static MethodInfo ResolveBuildHelloRequest()
        {
            var method = typeof(WsClient).GetMethod("BuildClientHelloRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected WsClient.BuildClientHelloRequest to exist.");
            return method;
        }

        [Test]
        public void HandleMessage_RoutesSceneQueryAvatarsToSceneApiRequest()
        {
            var go = new GameObject("WsClientTests");
            string routedJson = null;
            System.Action<string> handler = payload => routedJson = payload;

            try
            {
                var client = go.AddComponent<WsClient>();
                WsClient.OnSceneApiRequest += handler;

                ResolveHandleMessage().Invoke(client, new object[]
                {
                    "{\"type\":\"scene.query_avatars\",\"request_id\":\"qry_avatar_1\"}"
                });

                Assert.That(routedJson, Is.EqualTo("{\"type\":\"scene.query_avatars\",\"request_id\":\"qry_avatar_1\"}"));
            }
            finally
            {
                WsClient.OnSceneApiRequest -= handler;
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HandleMessage_RoutesSceneQueryAvatarAttachmentPointsToSceneApiRequest()
        {
            var go = new GameObject("WsClientTests");
            string routedJson = null;
            System.Action<string> handler = payload => routedJson = payload;

            try
            {
                var client = go.AddComponent<WsClient>();
                WsClient.OnSceneApiRequest += handler;

                ResolveHandleMessage().Invoke(client, new object[]
                {
                    "{\"type\":\"scene.query_avatar_attachment_points\",\"avatar_id\":\"avatar_main\",\"request_id\":\"qry_attach_1\"}"
                });

                Assert.That(routedJson, Is.EqualTo("{\"type\":\"scene.query_avatar_attachment_points\",\"avatar_id\":\"avatar_main\",\"request_id\":\"qry_attach_1\"}"));
            }
            finally
            {
                WsClient.OnSceneApiRequest -= handler;
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HandleMessage_LogsServerErrorWithoutUnknownMessageFallback()
        {
            var go = new GameObject("WsClientTests");

            try
            {
                var client = go.AddComponent<WsClient>();
                LogAssert.Expect(LogType.Warning, "[WS] Server error: TimeoutError\nTimed out waiting for scene.query_avatars");

                ResolveHandleMessage().Invoke(client, new object[]
                {
                    "{\"type\":\"error\",\"message\":\"TimeoutError\",\"details\":\"Timed out waiting for scene.query_avatars\"}"
                });
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildClientHelloRequest_IncludesUsernameAndUs()
        {
            var go = new GameObject("WsClientTests");

            try
            {
                var client = go.AddComponent<WsClient>();
                var request = ResolveBuildHelloRequest().Invoke(client, new object[] { "alice", "lab_a" }) as ClientHelloRequest;

                Assert.That(request, Is.Not.Null);
                Assert.That(request.type, Is.EqualTo("client.hello"));
                Assert.That(request.username, Is.EqualTo("alice"));
                Assert.That(request.us, Is.EqualTo("lab_a"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
