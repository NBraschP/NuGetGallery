﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class UserExtensionsFacts
    {
        public class TheToMailAddressMethod
        {
            [Fact]
            public void WhenUserIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensions.ToMailAddress(null);
                });
            }

            [Fact]
            public void WhenConfirmed_ReturnsEmailAddress()
            {
                // User.Confirmed is calculated property based on EmailAddress
                var user = new User("confirmed")
                {
                    EmailAddress = "confirmed@example.com"
                };

                var mailAddress = user.ToMailAddress();

                Assert.Equal("confirmed@example.com", mailAddress.Address);
                Assert.Equal("confirmed", mailAddress.User);
            }

            [Fact]
            public void WhenNotConfirmed_ReturnsUnconfirmedEmailAddress()
            {
                // User.Confirmed is calculated property based on EmailAddress
                var user = new User("unconfirmed")
                {
                    UnconfirmedEmailAddress = "unconfirmed@example.com"
                };

                var mailAddress = user.ToMailAddress();

                Assert.Equal("unconfirmed@example.com", mailAddress.Address);
                Assert.Equal("unconfirmed", mailAddress.User);
            }
        }

        public class TheGetCurrentApiKeyCredentialMethod
        {
            [Fact]
            public void WhenUserIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensions.GetCurrentApiKeyCredential(null, new ClaimsIdentity());
                });
            }

            [Theory]
            [InlineData("apikey.v2")]
            [InlineData("apikey.verify.v1")]
            public void ReturnsApiKeyMatchingClaim(string credentialType)
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(credentialType, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, "B"));

                // Act
                var credential = user.GetCurrentApiKeyCredential(identity);

                // Assert
                Assert.Equal("B", credential.Value);
            }

            [Fact]
            public void ReturnsNullIfNoApiKeyClaim()
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.LocalUser);

                // Act & Assert
                Assert.Null(user.GetCurrentApiKeyCredential(identity));
            }
        }

        public class TheIsOwnerOrMemberOfOrganizationOwnerMethod
        {
            [Fact]
            public void WhenUserIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensions.IsOwnerOrMemberOfOrganizationOwner(null, new PackageRegistration());
                });
            }

            [Fact]
            public void WhenPackageIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    new User().IsOwnerOrMemberOfOrganizationOwner(null);
                });
            }

            [Fact]
            public void WhenDirectOwner_ReturnsTrue()
            {
                var user = new User();
                var package = new PackageRegistration()
                {
                    Owners = new[] { user }
                };

                Assert.True(user.IsOwnerOrMemberOfOrganizationOwner(package));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WhenInDirectOwner_ReturnsTrue(bool isAdmin)
            {
                var user = new User();
                var organization = new Organization()
                {
                    Members = new []
                    {
                        new Membership()
                        {
                            Member = user,
                            IsAdmin = isAdmin
                        }
                    }
                };
                var package = new PackageRegistration()
                {
                    Owners = new[] { organization }
                };

                Assert.True(user.IsOwnerOrMemberOfOrganizationOwner(package));
            }

            [Fact]
            public void WhenNotOwner_ReturnsFalse()
            {
                var user = new User();
                var package = new PackageRegistration();

                Assert.False(user.IsOwnerOrMemberOfOrganizationOwner(package));
            }
        }

        public class TheMemberMatchesOwnerScopeMethod
        {
            [Fact]
            public void WhenUserIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensions.MatchesOwnerScope(null, new Credential());
                });
            }

            [Fact]
            public void WhenCredentialIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensions.MatchesOwnerScope(new User(), null);
                });
            }

            [Fact]
            public void WhenApiKeyWithNoScopes_ReturnsTrue()
            {
                var user = new User();
                // Legacy V1 with no scopes should match self or organizations.
                var credential = new Credential(CredentialTypes.ApiKey.V1, "");

                Assert.True(user.MatchesOwnerScope(credential));
            }

            [Fact]
            public void WhenApiKeyWithNoOwnerScope_ReturnsTrue()
            {
                var user = new User();
                // Legacy V2 with no owner scope should match self or organizations.
                var credential = new Credential(CredentialTypes.ApiKey.V2, "")
                {
                    Scopes = new[] { new Scope(null, "subject", "allowedAction") }
                };

                Assert.True(user.MatchesOwnerScope(credential));
            }

            [Fact]
            public void WhenApiKeyWithDirectMatchingOwnerScope_ReturnsTrue()
            {
                var user = new User() { Key = 1234 };
                var credential = new Credential(CredentialTypes.ApiKey.V2, "")
                {
                    Scopes = new[] { new Scope(1234, "subject", "allowedAction") }
                };

                Assert.True(user.MatchesOwnerScope(credential));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WhenApiKeyWithIndirectMatchingOwnerScope_ReturnsTrue(bool isAdmin)
            {
                var organization = new Organization() { Key = 2345 };
                var user = new User() { Key = 1234 };

                user.Organizations.Add(new Membership()
                {
                    Organization = organization,
                    OrganizationKey = organization.Key,
                    Member = user,
                    IsAdmin = isAdmin
                });

                var credential = new Credential(CredentialTypes.ApiKey.V2, "")
                {
                    Scopes = new[] { new Scope(2345, "subject", "allowedAction") }
                };

                Assert.True(user.MatchesOwnerScope(credential));
            }

            [Fact]
            public void WhenApiKeyWithNonMatchingOwnerScopes_ReturnsFalse()
            {
                var user = new User() { Key = 1234 };
                var credential = new Credential(CredentialTypes.ApiKey.V2, "")
                {
                    Scopes = new[] { new Scope(2345, "subject", "allowedAction") }
                };

                Assert.False(user.MatchesOwnerScope(credential));
            }
        }
    }
}
