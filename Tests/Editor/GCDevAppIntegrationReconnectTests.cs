using System.Collections;
using System.Reflection;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;

public sealed class GCDevAppIntegrationReconnectTests
{
    [Test]
    public void ConnectReEnablesReconnectAfterManualDisconnect()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            integration.Disconnect();
            Assert.That(GetShouldReconnect(integration), Is.False);

            SetIsConnecting(integration, true);
            integration.Connect();

            Assert.That(GetShouldReconnect(integration), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ScheduledReconnectStopsWhenReconnectIsSuppressedDuringDelay()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect suppression test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            var reconnectRoutine = InvokeScheduleReconnect(integration);
            Assert.That(reconnectRoutine.MoveNext(), Is.True);
            Assert.That(reconnectRoutine.Current, Is.TypeOf<WaitForSeconds>());

            integration.Disconnect();

            Assert.That(reconnectRoutine.MoveNext(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ScheduledReconnectUsesConnectGuardWhenAlreadyConnecting()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect stale schedule test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            SetIsConnecting(integration, true);
            var reconnectRoutine = InvokeScheduleReconnect(integration);

            Assert.That(reconnectRoutine.MoveNext(), Is.True);
            Assert.That(reconnectRoutine.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(reconnectRoutine.MoveNext(), Is.False);
            Assert.That(GetShouldReconnect(integration), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static bool GetShouldReconnect(GCDevAppIntegration integration)
    {
        return (bool)GetPrivateField("shouldReconnect").GetValue(integration);
    }

    private static void SetIsConnecting(GCDevAppIntegration integration, bool value)
    {
        GetPrivateField("isConnecting").SetValue(integration, value);
    }

    private static IEnumerator InvokeScheduleReconnect(GCDevAppIntegration integration)
    {
        return (IEnumerator)GetPrivateMethod("ScheduleReconnect").Invoke(integration, null);
    }

    private static FieldInfo GetPrivateField(string fieldName)
    {
        var field = typeof(GCDevAppIntegration).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Expected field {fieldName} to exist.");
        return field;
    }

    private static MethodInfo GetPrivateMethod(string methodName)
    {
        var method = typeof(GCDevAppIntegration).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null, $"Expected method {methodName} to exist.");
        return method;
    }
}
