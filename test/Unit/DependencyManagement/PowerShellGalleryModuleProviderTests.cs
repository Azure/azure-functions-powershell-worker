namespace Microsoft.Azure.Functions.PowerShellWorker.Test.DependencyManagement
{
    using System;
    using System.IO;
    using System.Text;
    using Moq;
    using Xunit;

    using PowerShellWorker.DependencyManagement;
    using PowerShellWorker.Utility;

    public class PowerShellGalleryModuleProviderTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private readonly Mock<IPowerShellGallerySearchInvoker> _mockSearchInvoker =
            new Mock<IPowerShellGallerySearchInvoker>(MockBehavior.Strict);

        private PowerShellGalleryModuleProvider _moduleProvider;

        public PowerShellGalleryModuleProviderTests()
        {
            _moduleProvider = new PowerShellGalleryModuleProvider(_mockLogger.Object, _mockSearchInvoker.Object);
        }

        [Fact]
        public void ReturnsNullIfSearchInvokerReturnsNull()
        {
            _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(default(Stream));
            var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
            Assert.Null(actualVersion);
        }

        [Fact]
        public void ReturnsSingleVersion()
        {
            const string ResponseText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.4</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Equal("1.2.3.4", actualVersion);
            }
        }

        [Fact]
        public void FindsLatestVersionRegardlessOfResponseOrder()
        {
            const string ResponseText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.4</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.6</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.5</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Equal("1.2.3.6", actualVersion);
            }
        }

        [Fact]
        public void IgnoresPrereleaseVersions()
        {
            const string ResponseText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.4</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.6</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">true</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.5</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Equal("1.2.3.5", actualVersion);
            }
        }

        [Fact]
        public void IgnoresNotMatchingMajorVersions()
        {
            const string ResponseText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>0.2.3.7</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.5</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.6</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>2.2.3.7</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Equal("1.2.3.6", actualVersion);
            }
        }

        [Theory]
        [InlineData("0.1", "0.2")]
        [InlineData("0.1", "0.1.0")]
        [InlineData("0.1", "0.1.1")]
        [InlineData("0.1.2", "0.2.1")]
        [InlineData("0.1.0", "0.1.0.1")]
        [InlineData("0.1.2.3", "0.1.2.4")]
        [InlineData("0.1.2.4", "0.2.2.3")]
        public void ComparesVersionsCorrectly(string lowerVersion, string higherVersion)
        {
            const string ResponseTextTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>{0}</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>{1}</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            var responseText = string.Format(ResponseTextTemplate, lowerVersion, higherVersion);
            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "0");
                Assert.Equal(higherVersion, actualVersion);
            }
        }

        [Fact]
        public void ReturnsNullIfNoVersionFound()
        {
            const string ResponseText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
</feed>";

            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsAny<Uri>())).Returns(responseStream);
                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Null(actualVersion);
            }
        }

        [Fact]
        public void FindsLatestVersionAcrossMultiplePages()
        {
            const string ResponseText1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <link rel=""next"" href=""https://NextLink1"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.4</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.5</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            const string ResponseText2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <link rel=""next"" href=""https://NextLink2"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.1</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.6</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            const string ResponseText3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed
    xml:base=""https://www.powershellgallery.com/api/v2""
    xmlns=""http://www.w3.org/2005/Atom""
    xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices""
    xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""https://www.powershellgallery.com/api/v2/Packages"" />
  <entry>
    <m:properties>
      <d:Version>1.2.3.2</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Version>1.2.3.3</d:Version>
      <d:IsPrerelease m:type=""Edm.Boolean"">false</d:IsPrerelease>
    </m:properties>
  </entry>
</feed>";

            using (var responseStream1 = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText1)))
            using (var responseStream2 = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText2)))
            using (var responseStream3 = new MemoryStream(Encoding.UTF8.GetBytes(ResponseText3)))
            {
                _mockSearchInvoker.Setup(_ => _.Invoke(It.IsNotIn(new Uri("https://NextLink1"), new Uri("https://NextLink2"))))
                    .Returns(responseStream1);

                _mockSearchInvoker.Setup(_ => _.Invoke(new Uri("https://NextLink1")))
                    .Returns(responseStream2);

                _mockSearchInvoker.Setup(_ => _.Invoke(new Uri("https://NextLink2")))
                    .Returns(responseStream3);

                var actualVersion = _moduleProvider.GetLatestPublishedModuleVersion("ModuleName", "1");
                Assert.Equal("1.2.3.6", actualVersion);
            }
        }
    }
}
