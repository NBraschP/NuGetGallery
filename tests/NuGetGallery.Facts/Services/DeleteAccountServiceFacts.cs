﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Authentication;
using NuGetGallery.Security;
using Xunit;
using Moq;


namespace NuGetGallery.Services
{
    public class DeleteAccountServiceFacts
    {
        public class TheDeleteGalleryUserAccountAsyncMethod
        {
            [Fact]
            public async Task NullUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(null, new User("AdminUser"), "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
            }

            [Fact]
            public async Task NullAdmin()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => deleteAccountService.DeleteGalleryUserAccountAsync(new User("TestUser"),null , "Signature", unlistOrphanPackages: true, commitAsTransaction: false));
            }

            /// <summary>
            /// The action to delete a deleted user will be noop.
            /// </summary>
            /// <returns></returns>
            [Fact]
            public async Task DeleteDeletedUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                testUser.IsDeleted = true;
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Act
                var signature = "Hello";
                var result = await deleteAccountService.
                    DeleteGalleryUserAccountAsync(userToBeDeleted: testUser,
                                                admin: testUser,
                                                signature: signature,
                                                unlistOrphanPackages: true,
                                                commitAsTransaction: false);
                string expected = $"The account:{testUser.Username} was already deleted. No action was performed.";
                Assert.Equal<string>(expected, result.Description);
            }

            /// <summary>
            /// One user with one package that has one namespace reserved and one security policy.
            /// After the account deletion:
            /// The user data(for example the email address) will be cleaned
            /// The package will be unlisted.
            /// The user will have the policies removed.
            /// The namespace will be unassigned from the user.
            /// The information about the deletion will be saved.
            /// </summary>
            /// <returns></returns>
            [Fact]
            public async Task DeleteHappyUser()
            {
                //Arange
                PackageRegistration registration = null;
                var testUser = CreateTestData(ref registration);
                var testableService = new DeleteAccountTestService(testUser, registration);
                var deleteAccountService = testableService.GetDeleteAccountService();

                //Act
                var signature = "Hello";
                await deleteAccountService.
                    DeleteGalleryUserAccountAsync(userToBeDeleted: testUser,
                                                admin: testUser,
                                                signature: signature,
                                                unlistOrphanPackages: true,
                                                commitAsTransaction: false);

                Assert.Equal<int>(0, registration.Owners.Count());
                Assert.Equal<int>(0, testUser.SecurityPolicies.Count());
                Assert.Equal<int>(0, testUser.ReservedNamespaces.Count());
                Assert.Equal<bool>(false, registration.Packages.ElementAt(0).Listed);
                Assert.Null(testUser.EmailAddress);
                Assert.Equal<int>(1, testableService.DeletedAccounts.Count());
                Assert.Equal<string>(signature, testableService.DeletedAccounts.ElementAt(0).Signature);
            }

            private static User CreateTestData(ref PackageRegistration registration)
            {
                User testUser = new User();
                testUser.Username = "TestsUser";
                testUser.EmailAddress = "user@test.com";

                registration = new PackageRegistration();
                registration.Owners.Add(testUser);

                Package p = new Package()
                {
                    Description = "TestPackage",
                    Key = 1
                };
                p.PackageRegistration = registration;
                registration.Packages.Add(p);
                return testUser;
            }
        }

        public class DeleteAccountTestService
        {
            private const string SubscriptionName = "SecPolicySubscription";
            private User _user = null;
            private static ReservedNamespace _reserverdNamespace = new ReservedNamespace("Ns1", false, false);
            private Credential _credential = new Credential("CredType", "CredValue");
            private UserSecurityPolicy _securityPolicy = new UserSecurityPolicy("PolicyName", SubscriptionName);
            private PackageRegistration _userPackagesRegistration = null;
            private ICollection<Package> _userPackages;

            public List<AccountDelete> DeletedAccounts = new List<AccountDelete>();

            public DeleteAccountTestService(User user, PackageRegistration userPackagesRegistration)
            {
                _user = user;
                _user.ReservedNamespaces.Add(_reserverdNamespace);
                _user.Credentials.Add(_credential);
                _user.SecurityPolicies.Add(_securityPolicy);
                _userPackagesRegistration = userPackagesRegistration;
                _userPackages = userPackagesRegistration.Packages;
            }

            public DeleteAccountService GetDeleteAccountService()
            {
                return new DeleteAccountService(SetupAccountDeleteRepository().Object,
                    SetupUserRepository().Object,
                    SetupEntitiesContext().Object,
                    SetupPackageService().Object,
                    SetupPackageOwnershipManagementService().Object,
                    SetupReservedNamespaceService().Object,
                    SetupSecurityPolicyService().Object,
                    new TestableAuthService());
            }

            private class TestableAuthService : AuthenticationService
            {
                public TestableAuthService() : base()
                { }

                public override async Task AddCredential(User user, Credential credential)
                {
                    await Task.Yield();
                    user.Credentials.Add(credential);
                }

                public override async Task RemoveCredential(User user, Credential credential)
                {
                    await Task.Yield();
                    user.Credentials.Remove(credential);
                }
            }

            private Mock<IEntitiesContext> SetupEntitiesContext()
            {
                var mockContext = new Mock<IEntitiesContext>();
                var dbContext = new Mock<DbContext>();
                mockContext.Setup(m => m.GetDatabase()).Returns(dbContext.Object.Database);
                return mockContext;
            }

            private Mock<IReservedNamespaceService> SetupReservedNamespaceService()
            {
                var namespaceService = new Mock<IReservedNamespaceService>();
                namespaceService.Setup(m => m.DeleteOwnerFromReservedNamespaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                                .Returns(Task.CompletedTask)
                                .Callback(() => _user.ReservedNamespaces.Remove(_reserverdNamespace));
                return namespaceService;
            }

            private Mock<ISecurityPolicyService> SetupSecurityPolicyService()
            {
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService.Setup(m => m.UnsubscribeAsync(It.IsAny<User>(), SubscriptionName))
                                     .Returns(Task.CompletedTask)
                                     .Callback(() => _user.SecurityPolicies.Remove(_securityPolicy));
                return securityPolicyService;
            }

            private Mock<IEntityRepository<AccountDelete>> SetupAccountDeleteRepository()
            {
                var acountDeleteRepository = new Mock<IEntityRepository<AccountDelete>>();
                acountDeleteRepository.Setup(m => m.InsertOnCommit(It.IsAny<AccountDelete>()))
                                      .Callback<AccountDelete>(account => DeletedAccounts.Add(account));
                return acountDeleteRepository;
            }

            private Mock<IEntityRepository<User>> SetupUserRepository()
            {
                var userRepository = new Mock<IEntityRepository<User>>();
                userRepository.Setup(m => m.CommitChangesAsync())
                              .Returns(Task.CompletedTask);
                return userRepository;
            }

            private Mock<IPackageService> SetupPackageService()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(m => m.FindPackagesByOwner(_user, true)).Returns(_userPackages);
                //the .Returns(Task.CompletedTask) to avoid NullRef exception by the Mock infrastructure when invoking async operations
                packageService.Setup(m => m.MarkPackageUnlistedAsync(It.IsAny<Package>(), true))
                              .Returns(Task.CompletedTask)
                              .Callback<Package, bool>((package, commit) => { package.Listed = false; });
                return packageService;
            }

            private Mock<IPackageOwnershipManagementService> SetupPackageOwnershipManagementService()
            {
                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                packageOwnershipManagementService.Setup(m => m.RemovePackageOwnerAsync(It.IsAny<PackageRegistration>(), It.IsAny<User>(), It.IsAny<User>(), false))
                                                 .Returns(Task.CompletedTask)
                                                 .Callback(() => 
                                                 {
                                                     _userPackagesRegistration.Owners.Remove(_user);
                                                     _userPackagesRegistration.ReservedNamespaces.Remove(_reserverdNamespace);
                                                 }
                                                            );
                return packageOwnershipManagementService;
            }

        }
    }
}
