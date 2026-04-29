// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Runtime.Serialization;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ClientCitationIconNameTests
    {
        // ── Equality ──────────────────────────────────────────────────────────

        [Fact]
        public void Equals_SameKnownValue_ReturnsTrue()
        {
            Assert.Equal(ClientCitationIconName.MicrosoftWord, ClientCitationIconName.MicrosoftWord);
        }

        [Fact]
        public void Equals_DifferentKnownValues_ReturnsFalse()
        {
            Assert.NotEqual(ClientCitationIconName.MicrosoftWord, ClientCitationIconName.PDF);
        }

        [Fact]
        public void Equals_CaseInsensitive_ReturnsTrue()
        {
            ClientCitationIconName a = "microsoft word";
            ClientCitationIconName b = "MICROSOFT WORD";
            Assert.Equal(a, b);
        }

        [Fact]
        public void OperatorEquality_KnownValues_IsTrue()
        {
            Assert.True(ClientCitationIconName.PDF == new ClientCitationIconName(ClientCitationIconName.Names.PDF));
        }

        [Fact]
        public void OperatorInequality_DifferentValues_IsTrue()
        {
            Assert.True(ClientCitationIconName.PDF != ClientCitationIconName.MicrosoftWord);
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            Assert.False(ClientCitationIconName.MicrosoftWord.Equals(null));
        }

        // ── Implicit conversions ──────────────────────────────────────────────

        [Fact]
        public void ImplicitFromString_RoundTripsCorrectly()
        {
            ClientCitationIconName name = "Custom Value";
            string back = name;
            Assert.Equal("Custom Value", back);
        }

        [Fact]
        public void ImplicitToString_KnownValue_ReturnsExpected()
        {
            string value = ClientCitationIconName.MicrosoftExcel;
            Assert.Equal(ClientCitationIconName.Names.MicrosoftExcel, value);
        }

        [Fact]
        public void ToString_KnownValue_ReturnsStringValue()
        {
            Assert.Equal(ClientCitationIconName.Names.SourceCode, ClientCitationIconName.SourceCode.ToString());
        }

        [Fact]
        public void ToString_NullWrapped_ReturnsEmptyString()
        {
            ClientCitationIconName name = new ClientCitationIconName(null);
            Assert.Equal(string.Empty, name.ToString());
        }

        // ── GetHashCode ───────────────────────────────────────────────────────

        [Fact]
        public void GetHashCode_EqualValues_SameHash()
        {
            ClientCitationIconName a = "PDF";
            ClientCitationIconName b = "pdf";
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullWrapped_ReturnsZero()
        {
            var name = new ClientCitationIconName(null);
            Assert.Equal(0, name.GetHashCode());
        }

        // ── Serialization (Write) ─────────────────────────────────────────────

        [Fact]
        public void Serialize_KnownValue_WritesStringValue()
        {
            var obj = new ClientCitationIconNameWrapper { Name = ClientCitationIconName.MicrosoftWord };
            string json = JsonSerializer.Serialize(obj);
            Assert.Contains($"\"{ClientCitationIconName.Names.MicrosoftWord}\"", json);
        }

        [Fact]
        public void Serialize_ArbitraryValue_WritesStringValue()
        {
            ClientCitationIconName name = "My Custom Icon";
            var obj = new ClientCitationIconNameWrapper { Name = name };
            string json = JsonSerializer.Serialize(obj);
            Assert.Contains("\"My Custom Icon\"", json);
        }

        // ── Deserialization (Read) ────────────────────────────────────────────

        [Fact]
        public void Deserialize_KnownValue_EqualsStaticInstance()
        {
            string json = $"{{\"Name\":\"{ClientCitationIconName.Names.PDF}\"}}";
            var obj = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
            Assert.Equal(ClientCitationIconName.PDF, obj!.Name);
        }

        [Fact]
        public void Deserialize_UnknownValue_DoesNotThrow()
        {
            // Unknown names must deserialize without throwing — this is the main
            // advantage of the string-enum pattern over a plain enum.
            string json = "{\"Name\":\"Some Future Icon\"}";
            var obj = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
            Assert.NotNull(obj);
            Assert.Equal("Some Future Icon", obj!.Name!.ToString());
        }

        [Fact]
        public void Deserialize_AllKnownNames_DeserializeCorrectly()
        {
            string[] knownNames =
            [
                ClientCitationIconName.Names.MicrosoftWord,
                ClientCitationIconName.Names.MicrosoftExcel,
                ClientCitationIconName.Names.MicrosoftPowerPoint,
                ClientCitationIconName.Names.MicrosoftVisio,
                ClientCitationIconName.Names.MicrosoftLoop,
                ClientCitationIconName.Names.MicrosoftWhiteboard,
                ClientCitationIconName.Names.AdobeIllustrator,
                ClientCitationIconName.Names.AdobePhotoshop,
                ClientCitationIconName.Names.AdobeInDesign,
                ClientCitationIconName.Names.AdobeFlash,
                ClientCitationIconName.Names.Sketch,
                ClientCitationIconName.Names.SourceCode,
                ClientCitationIconName.Names.Image,
                ClientCitationIconName.Names.GIF,
                ClientCitationIconName.Names.Video,
                ClientCitationIconName.Names.Sound,
                ClientCitationIconName.Names.ZIP,
                ClientCitationIconName.Names.Text,
                ClientCitationIconName.Names.PDF,
            ];

            foreach (var name in knownNames)
            {
                string json = $"{{\"Name\":\"{name}\"}}";
                var obj = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
                Assert.NotNull(obj);
                Assert.Equal(name, obj!.Name!.ToString());
            }
        }

        [Fact]
        public void Deserialize_NullToken_ReturnsNull()
        {
            string json = "{\"Name\":null}";
            var obj = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
            Assert.Null(obj!.Name);
        }

        // ── Round-trip ────────────────────────────────────────────────────────

        [Fact]
        public void RoundTrip_KnownValue_Preserved()
        {
            var original = new ClientCitationIconNameWrapper { Name = ClientCitationIconName.GIF };
            string json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
            Assert.Equal(original.Name, restored!.Name);
        }

        [Fact]
        public void RoundTrip_UnknownValue_Preserved()
        {
            ClientCitationIconName unknownName = "Brand New Icon";
            var original = new ClientCitationIconNameWrapper { Name = unknownName };
            string json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<ClientCitationIconNameWrapper>(json);
            Assert.Equal(unknownName, restored!.Name);
        }

        // ── Names static class ────────────────────────────────────────────────

        [Fact]
        public void Names_StaticConstants_MatchStaticInstances()
        {
            Assert.Equal(ClientCitationIconName.Names.MicrosoftWord, (string)ClientCitationIconName.MicrosoftWord);
            Assert.Equal(ClientCitationIconName.Names.MicrosoftExcel, (string)ClientCitationIconName.MicrosoftExcel);
            Assert.Equal(ClientCitationIconName.Names.PDF, (string)ClientCitationIconName.PDF);
        }

        [Fact]
        public void ClientCitation_With_ClientCitationsIconNameEnum()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var citation = new ClientCitation(0, "title", "abstractText", "text", null, null, ClientCitationsIconNameEnum.MicrosoftPowerPoint);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.Equal(ClientCitationIconName.Names.MicrosoftPowerPoint, (string)citation.Appearance.Image.Name);
        }

        // ── Helper wrapper for JSON tests ─────────────────────────────────────

        private sealed class ClientCitationIconNameWrapper
        {
            public ClientCitationIconName Name { get; set; }
        }
    }
}
