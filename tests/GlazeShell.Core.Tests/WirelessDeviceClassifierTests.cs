using System;
using GlazeShell.Core.SystemStatus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class WirelessDeviceClassifierTests
{
    /// <summary>Профиль «свободные руки»: им помечаются и наушники, и гарнитуры.</summary>
    private const string HandsFreeProfile =
        @"BTHENUM\{0000111E-0000-1000-8000-00805F9B34FB}_VID&000102B0_PID&0000\8&377BE277&0&2CBEEB71B87E_C00000000";

    /// <summary>Профиль A2DP — передача звука на наушники и колонки.</summary>
    private const string AudioSinkProfile =
        @"BTHENUM\{0000110B-0000-1000-8000-00805F9B34FB}_VID&000102B0_PID&0000\8&377BE277&0&2CBEEB71B87E_C00000000";

    [TestMethod]
    public void AudioProfileMeansHeadphonesEvenWhenTheNameIsJustAModel()
    {
        var kind = WirelessDeviceClassifier.Classify("Ear (2)", [HandsFreeProfile, AudioSinkProfile]);

        Assert.AreEqual(WirelessDeviceKind.Headphones, kind);
    }

    [TestMethod]
    public void SpeakerNameTurnsAudioProfileIntoASpeaker()
    {
        var kind = WirelessDeviceClassifier.Classify("Колонка JBL Charge", [AudioSinkProfile]);

        Assert.AreEqual(WirelessDeviceKind.Speaker, kind);
    }

    [TestMethod]
    public void AudioProfileIsRecognizedRegardlessOfCase()
    {
        var kind = WirelessDeviceClassifier.Classify(
            "Наушники",
            [@"bthenum\{00001108-0000-1000-8000-00805f9b34fb}_vid&0001\8&0&0"]);

        Assert.AreEqual(WirelessDeviceKind.Headphones, kind);
    }

    [TestMethod]
    public void MouseIsRecognizedByName()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Mouse,
            WirelessDeviceClassifier.Classify("Logitech MX Master 3", []));
        Assert.AreEqual(
            WirelessDeviceKind.Mouse,
            WirelessDeviceClassifier.Classify("Беспроводная мышь", null));
    }

    [TestMethod]
    public void KeyboardIsRecognizedByName()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Keyboard,
            WirelessDeviceClassifier.Classify("Surface Keyboard", []));
        Assert.AreEqual(
            WirelessDeviceKind.Keyboard,
            WirelessDeviceClassifier.Classify("Клавиатура Keychron", []));
    }

    [TestMethod]
    public void GamepadIsRecognizedByName()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Gamepad,
            WirelessDeviceClassifier.Classify("Xbox Wireless Controller", []));
    }

    [TestMethod]
    public void PhoneIsRecognizedByName()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Phone,
            WirelessDeviceClassifier.Classify("Смартфон", []));
        Assert.AreEqual(
            WirelessDeviceKind.Phone,
            WirelessDeviceClassifier.Classify("Мой телефон", []));
    }

    [TestMethod]
    public void HeadphonesAreRecognizedByNameWithoutAProfile()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Headphones,
            WirelessDeviceClassifier.Classify("Беспроводные наушники", []));
    }

    [TestMethod]
    public void UnknownDeviceFallsBackToOther()
    {
        Assert.AreEqual(
            WirelessDeviceKind.Other,
            WirelessDeviceClassifier.Classify("Служба идентификации устройств", []));
        Assert.AreEqual(WirelessDeviceKind.Other, WirelessDeviceClassifier.Classify(null, null));
        Assert.AreEqual(WirelessDeviceKind.Other, WirelessDeviceClassifier.Classify("   ", []));
    }
}
