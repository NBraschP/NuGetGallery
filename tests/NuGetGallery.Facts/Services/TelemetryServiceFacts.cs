﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Moq;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;

using TrackAction = System.Action<NuGetGallery.TelemetryService>;

namespace NuGetGallery
{
    public class TelemetryServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsIfDiagnosticsServiceIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => new TelemetryService(null));
            }
        }

        public class TheTrackEventMethod : BaseFacts
        {
            private static Fakes fakes = new Fakes();

            public static IEnumerable<object[]> TrackEventNames_Data
            {
                get
                {
                    var package = fakes.Package.Packages.First();
                    var identity = Fakes.ToIdentity(fakes.User);

                    yield return new object[] { "ODataQueryFilter",
                        (TrackAction)(s => s.TrackODataQueryFilterEvent("callContext", true, true, "queryPattern"))
                    };

                    yield return new object[] { "PackagePush",
                        (TrackAction)(s => s.TrackPackagePushEvent(package, fakes.User, identity))
                    };

                    yield return new object[] { "CreatePackageVerificationKey",
                        (TrackAction)(s => s.TrackCreatePackageVerificationKeyEvent(fakes.Package.Id, package.Version, fakes.User, identity))
                    };

                    yield return new object[] { "VerifyPackageKey",
                        (TrackAction)(s => s.TrackVerifyPackageKeyEvent(fakes.Package.Id, package.Version, fakes.User, identity, 0))
                    };

                    yield return new object[] { "PackageReadMeChanged",
                        (TrackAction)(s => s.TrackPackageReadMeChangeEvent(package, "written", PackageEditReadMeState.Changed))
                    };

                    yield return new object[] { "PackagePushNamespaceConflict",
                        (TrackAction)(s => s.TrackPackagePushNamespaceConflictEvent(fakes.Package.Id, package.Version, fakes.User, identity))
                    };
                }
            }

            [Fact]
            public void TrackEventNamesIncludesAllEvents()
            {
                var count = typeof(TelemetryService.Events).GetFields().Length;
                var testData = TrackEventNames_Data;

                Assert.Equal(count, testData.Count());
            }

            [Theory]
            [MemberData(nameof(TrackEventNames_Data))]
            public void TrackEventNames(string eventName, TrackAction track)
            {
                // Arrange
                var service = CreateService();

                // Act
                track(service);

                // Assert
                service.TelemetryClient.Verify(c => c.TrackEvent(eventName,
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<IDictionary<string, double>>()),
                    Times.Once);
            }
            
            [Fact]
            public void TrackPackageReadMeChangeEventThrowsIfPackageIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackageReadMeChangeEvent(null, "written", PackageEditReadMeState.Changed));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void TrackPackageReadMeChangeEventThrowsIfSourceTypeIsNullOrEmpty(string sourceType)
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackageReadMeChangeEvent(fakes.Package.Packages.First(), sourceType, PackageEditReadMeState.Changed));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfPackageIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(null, fakes.User, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(fakes.Package.Packages.First(), null, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(fakes.Package.Packages.First(), fakes.User, null));
            }

            [Fact]
            public void TrackCreatePackageVerificationKeyEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackCreatePackageVerificationKeyEvent("id", "1.0.0", null, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackCreatePackageVerificationKeyEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackCreatePackageVerificationKeyEvent("id", "1.0.0", fakes.User, null));
            }

            [Fact]
            public void TrackVerifyPackageKeyEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackVerifyPackageKeyEvent("id", "1.0.0", null, Fakes.ToIdentity(fakes.User), 200));
            }

            [Fact]
            public void TrackVerifyPackageKeyEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackVerifyPackageKeyEvent("id", "1.0.0", fakes.User, null, 200));
            }
        }

        public class TheTraceExceptionMethod : BaseFacts
        {
            [Fact]
            public void ThrowsIfExceptionIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => service.TraceException(null));
            }

            [Fact]
            public void CallsTraceEvent()
            {
                // Arrange
                var service = CreateService();

                // Act
                service.TraceException(new InvalidOperationException("Example"));

                // Assert
                service.TraceSource.Verify(t => t.TraceEvent(
                        TraceEventType.Warning,
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()),
                    Times.Once);
                Assert.True(service.LastTraceMessage.Contains("InvalidOperationException"));
            }
        }

        public class BaseFacts
        {
            public class TelemetryServiceWrapper : TelemetryService
            {
                public TelemetryServiceWrapper(IDiagnosticsService diagnosticsService, ITelemetryClient telemetryClient)
                    : base(diagnosticsService, telemetryClient)
                {
                }

                public Mock<IDiagnosticsSource> TraceSource { get; set; }

                public Mock<ITelemetryClient> TelemetryClient { get; set; }

                public string LastTraceMessage { get; set; }
            }

            public TelemetryServiceWrapper CreateService()
            {
                var traceSource = new Mock<IDiagnosticsSource>();
                var traceService = new Mock<IDiagnosticsService>();
                var telemetryClient = new Mock<ITelemetryClient>();

                traceService.Setup(s => s.GetSource(It.IsAny<string>()))
                    .Returns(traceSource.Object);

                telemetryClient.Setup(c => c.TrackEvent(
                        It.IsAny<string>(),
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<IDictionary<string, double>>()))
                    .Verifiable();

                var telemetryService = new TelemetryServiceWrapper(traceService.Object, telemetryClient.Object);

                telemetryService.TraceSource = traceSource;
                telemetryService.TelemetryClient = telemetryClient;

                traceSource.Setup(t => t.TraceEvent(
                        It.IsAny<TraceEventType>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()))
                    .Callback<TraceEventType, int, string, string, string, int>(
                        (type, id, message, member, file, line) => telemetryService.LastTraceMessage = message)
                    .Verifiable();

                return telemetryService;
            }
        }
    }
}
