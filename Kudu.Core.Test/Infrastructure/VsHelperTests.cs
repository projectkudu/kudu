using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class VsHelperFacts
    {
        [Theory]
        [InlineData("netcoreapp3.1", "netcoreapp3.1")]
        [InlineData("netcoreapp3.0", "netcoreapp3.0")]
        [InlineData("netcoreapp2.2", "netcoreapp2.2")]
        [InlineData("netstandard2.1", "netstandard2.1")]
        [InlineData("netstandard2.0", "netstandard2.0")]
        public void GetTargetFramework(string targetFramework, string expected)
        {
            // Arrange
            var CsprojFileContent = string.Format(
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{0}</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Content Include=""run.bat"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
", targetFramework);

            // Act
            var result = VsHelper.GetTargetFrameworkContents(CsprojFileContent);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("netstandard2.0;netcoreapp2.2", "netstandard2.0")]
        [InlineData("netcoreapp3.1;netstandard2.1", "netcoreapp3.1")]
        [InlineData("netcoreapp2.1;netstandard3.1", "netcoreapp2.1")]
        public void GetTargetFrameworks(string targetFrameworks, string expected)
        {
            // Arrange
            var CsprojFileContent = string.Format(
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>{0}</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <Content Include=""run.bat"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
", targetFrameworks);

            // Act
            var result = VsHelper.GetTargetFrameworkContents(CsprojFileContent);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetTargetFramework_DotNetFramework()
        {
            // Arrange
            var CsprojFileContent =
@"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <DebugType>full</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore"" Version=""2.2.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.CookiePolicy"" Version=""2.2.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.HttpsPolicy"" Version=""2.2.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Mvc"" Version=""2.2.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.StaticFiles"" Version=""2.2.0"" />
  </ItemGroup>

</Project>
";

            // Act
            var result = VsHelper.GetTargetFrameworkContents(CsprojFileContent);

            // Assert
            Assert.Equal(result, "net472");
        }

        [Fact]
        public void GetTargetFramework_OldStyle()
        {
            // Arrange
            var CsprojFileContent =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProductVersion>
    </ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{32DCD27D-A84C-4250-B657-408B3620A9AC}</ProjectGuid>
    <ProjectTypeGuids>{E53F8FEA-EAE0-44A6-8774-FFD645390401};{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>MvcMusicStore</RootNamespace>
    <AssemblyName>MvcMusicStore</AssemblyName>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
    <MvcBuildViews>false</MvcBuildViews>
    <UseIISExpress>false</UseIISExpress>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""EntityFramework, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, processorArchitecture=MSIL"">
      <SpecificVersion>False</SpecificVersion>
      <HintPath>..\packages\EntityFramework.4.1.10331.0\lib\EntityFramework.dll</HintPath>
    </Reference>
    <Reference Include=""System.Data.Entity"" />
    <Reference Include=""System.Data.SqlServerCe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91, processorArchitecture=MSIL"">
      <Private>True</Private>
      <HintPath>..\packages\SqlServerCompact.4.0.8482.1\lib\System.Data.SqlServerCe.dll</HintPath>
    </Reference>
    <Reference Include=""System.Data.SqlServerCe.Entity, Version=4.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91, processorArchitecture=MSIL"">
      <Private>True</Private>
      <HintPath>..\packages\EntityFramework.SqlServerCompact.4.1.8482.1\lib\System.Data.SqlServerCe.Entity.dll</HintPath>
    </Reference>
    <Reference Include=""System.Transactions"" />
    <Reference Include=""System.Web.Mvc, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL"" />
    <Reference Include=""System.Web.WebPages, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL"" />
    <Reference Include=""System.Web.Helpers, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Drawing"" />
    <Reference Include=""System.Web.DynamicData"" />
    <Reference Include=""System.Web.Entity"" />
    <Reference Include=""System.Web.ApplicationServices"" />
    <Reference Include=""System.ComponentModel.DataAnnotations"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Web"" />
    <Reference Include=""System.Web.Extensions"" />
    <Reference Include=""System.Web.Abstractions"" />
    <Reference Include=""System.Web.Routing"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""System.Configuration"" />
    <Reference Include=""System.Web.Services"" />
    <Reference Include=""System.EnterpriseServices"" />
    <Reference Include=""WebActivator"">
      <HintPath>..\packages\WebActivator.1.0.0.0\lib\WebActivator.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""App_Start\EntityFramework.SqlServerCompact.cs"" />
    <Compile Include=""Controllers\AccountController.cs"" />
    <Compile Include=""Controllers\CheckoutController.cs"" />
    <Compile Include=""Controllers\HomeController.cs"" />
    <Compile Include=""Controllers\ShoppingCartController.cs"" />
    <Compile Include=""Controllers\StoreController.cs"" />
    <Compile Include=""Controllers\StoreManagerController.cs"" />
    <Compile Include=""Global.asax.cs"">
      <DependentUpon>Global.asax</DependentUpon>
    </Compile>
    <Compile Include=""Models\AccountModels.cs"" />
    <Compile Include=""Models\Album.cs"" />
    <Compile Include=""Models\Artist.cs"" />
    <Compile Include=""Models\Cart.cs"" />
    <Compile Include=""Models\Genre.cs"" />
    <Compile Include=""Models\MusicStoreEntities.cs"" />
    <Compile Include=""Models\Order.cs"" />
    <Compile Include=""Models\OrderDetail.cs"" />
    <Compile Include=""Models\SampleData.cs"" />
    <Compile Include=""Models\ShoppingCart.cs"" />
    <Compile Include=""Properties\AssemblyInfo.cs"" />
    <Compile Include=""ViewModels\ShoppingCartRemoveViewModel.cs"" />
    <Compile Include=""ViewModels\ShoppingCartViewModel.cs"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""App_Data\MvcMusicStore.sdf"" />
    <Content Include=""bin\amd64\Microsoft.VC90.CRT\msvcr90.dll"" />
    <Content Include=""bin\amd64\Microsoft.VC90.CRT\README_ENU.txt"" />
    <Content Include=""bin\amd64\sqlcecompact40.dll"" />
    <Content Include=""bin\amd64\sqlceer40EN.dll"" />
    <Content Include=""bin\amd64\sqlceme40.dll"" />
    <Content Include=""bin\amd64\sqlceqp40.dll"" />
    <Content Include=""bin\amd64\sqlcese40.dll"" />
    <Content Include=""bin\x86\Microsoft.VC90.CRT\msvcr90.dll"" />
    <Content Include=""bin\x86\Microsoft.VC90.CRT\README_ENU.txt"" />
    <Content Include=""bin\x86\sqlcecompact40.dll"" />
    <Content Include=""bin\x86\sqlceer40EN.dll"" />
    <Content Include=""bin\x86\sqlceme40.dll"" />
    <Content Include=""bin\x86\sqlceqp40.dll"" />
    <Content Include=""bin\x86\sqlcese40.dll"" />
    <Content Include=""Content\Images\home-showcase.png"" />
    <Content Include=""Content\Images\logo.png"" />
    <Content Include=""Content\Images\placeholder.gif"" />
    <Content Include=""Content\themes\base\images\ui-bg_flat_0_aaaaaa_40x100.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_flat_75_ffffff_40x100.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_glass_55_fbf9ee_1x400.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_glass_65_ffffff_1x400.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_glass_75_dadada_1x400.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_glass_75_e6e6e6_1x400.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_glass_95_fef1ec_1x400.png"" />
    <Content Include=""Content\themes\base\images\ui-bg_highlight-soft_75_cccccc_1x100.png"" />
    <Content Include=""Content\themes\base\images\ui-icons_222222_256x240.png"" />
    <Content Include=""Content\themes\base\images\ui-icons_2e83ff_256x240.png"" />
    <Content Include=""Content\themes\base\images\ui-icons_454545_256x240.png"" />
    <Content Include=""Content\themes\base\images\ui-icons_888888_256x240.png"" />
    <Content Include=""Content\themes\base\images\ui-icons_cd0a0a_256x240.png"" />
    <Content Include=""Content\themes\base\jquery.ui.accordion.css"" />
    <Content Include=""Content\themes\base\jquery.ui.all.css"" />
    <Content Include=""Content\themes\base\jquery.ui.autocomplete.css"" />
    <Content Include=""Content\themes\base\jquery.ui.base.css"" />
    <Content Include=""Content\themes\base\jquery.ui.button.css"" />
    <Content Include=""Content\themes\base\jquery.ui.core.css"" />
    <Content Include=""Content\themes\base\jquery.ui.datepicker.css"" />
    <Content Include=""Content\themes\base\jquery.ui.dialog.css"" />
    <Content Include=""Content\themes\base\jquery.ui.progressbar.css"" />
    <Content Include=""Content\themes\base\jquery.ui.resizable.css"" />
    <Content Include=""Content\themes\base\jquery.ui.selectable.css"" />
    <Content Include=""Content\themes\base\jquery.ui.slider.css"" />
    <Content Include=""Content\themes\base\jquery.ui.tabs.css"" />
    <Content Include=""Content\themes\base\jquery.ui.theme.css"" />
    <Content Include=""Global.asax"" />
    <Content Include=""Content\Site.css"" />
    <Content Include=""Scripts\jquery-1.5.1-vsdoc.js"" />
    <Content Include=""Scripts\jquery-1.5.1.js"" />
    <Content Include=""Scripts\jquery-1.5.1.min.js"" />
    <Content Include=""Scripts\jquery-ui-1.8.11.js"" />
    <Content Include=""Scripts\jquery-ui-1.8.11.min.js"" />
    <Content Include=""Scripts\jquery.validate-vsdoc.js"" />
    <Content Include=""Scripts\jquery.validate.js"" />
    <Content Include=""Scripts\jquery.validate.min.js"" />
    <Content Include=""Scripts\modernizr-1.7.js"" />
    <Content Include=""Scripts\modernizr-1.7.min.js"" />
    <Content Include=""Web.config"">
      <SubType>Designer</SubType>
    </Content>
    <Content Include=""Web.Debug.config"">
      <DependentUpon>Web.config</DependentUpon>
    </Content>
    <Content Include=""Web.Release.config"">
      <DependentUpon>Web.config</DependentUpon>
    </Content>
    <Content Include=""Scripts\jquery.unobtrusive-ajax.js"" />
    <Content Include=""Scripts\jquery.unobtrusive-ajax.min.js"" />
    <Content Include=""Scripts\jquery.validate.unobtrusive.js"" />
    <Content Include=""Scripts\jquery.validate.unobtrusive.min.js"" />
    <Content Include=""Scripts\MicrosoftAjax.js"" />
    <Content Include=""Scripts\MicrosoftAjax.debug.js"" />
    <Content Include=""Scripts\MicrosoftMvcAjax.js"" />
    <Content Include=""Scripts\MicrosoftMvcAjax.debug.js"" />
    <Content Include=""Scripts\MicrosoftMvcValidation.js"" />
    <Content Include=""Scripts\MicrosoftMvcValidation.debug.js"" />
    <Content Include=""Views\Web.config"" />
    <Content Include=""Views\_ViewStart.cshtml"" />
    <Content Include=""Views\Shared\Error.cshtml"" />
    <Content Include=""Views\Shared\_Layout.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Store\Details.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""packages.config"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Home\Index.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Store\Browse.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Store\Index.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <None Include=""bin\amd64\Microsoft.VC90.CRT\Microsoft.VC90.CRT.manifest"" />
    <None Include=""bin\x86\Microsoft.VC90.CRT\Microsoft.VC90.CRT.manifest"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\StoreManager\Index.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\StoreManager\Details.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\StoreManager\Create.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\StoreManager\Edit.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\StoreManager\Delete.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Account\ChangePassword.cshtml"" />
    <Content Include=""Views\Account\ChangePasswordSuccess.cshtml"" />
    <Content Include=""Views\Account\LogOn.cshtml"" />
    <Content Include=""Views\Account\Register.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\ShoppingCart\Index.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Checkout\AddressAndPayment.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Checkout\Complete.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\ShoppingCart\CartSummary.cshtml"" />
  </ItemGroup>
  <ItemGroup>
    <Content Include=""Views\Store\GenreMenu.cshtml"" />
  </ItemGroup>
  <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
  <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target> -->
  <Target Name=""MvcBuildViews"" AfterTargets=""AfterBuild"" Condition=""'$(MvcBuildViews)'=='true'"">
    <AspNetCompiler VirtualPath=""temp"" PhysicalPath=""$(WebProjectOutputDir)"" />
  </Target>
  <ProjectExtensions>
    <VisualStudio>
      <FlavorProperties GUID=""{349c5851-65df-11da-9384-00065b846f21}"">
        <WebProjectProperties>
          <UseIIS>False</UseIIS>
          <AutoAssignPort>False</AutoAssignPort>
          <DevelopmentServerPort>26641</DevelopmentServerPort>
          <DevelopmentServerVPath>/</DevelopmentServerVPath>
          <IISUrl>
          </IISUrl>
          <NTLMAuthentication>False</NTLMAuthentication>
          <UseCustomServer>False</UseCustomServer>
          <CustomServerUrl>
          </CustomServerUrl>
          <SaveServerSettingsInUserFile>False</SaveServerSettingsInUserFile>
        </WebProjectProperties>
      </FlavorProperties>
    </VisualStudio>
  </ProjectExtensions>
  <PropertyGroup>
    <PostBuildEvent>
if not exist ""$(TargetDir)x86"" md ""$(TargetDir)x86""
xcopy /s /y ""$(ProjectDir)..\packages\SqlServerCompact.4.0.8482.1\NativeBinaries\x86\*.*"" ""$(TargetDir)x86""
if not exist ""$(TargetDir)amd64"" md ""$(TargetDir)amd64""
xcopy /s /y ""$(ProjectDir)..\packages\SqlServerCompact.4.0.8482.1\NativeBinaries\amd64\*.*"" ""$(TargetDir)amd64""</PostBuildEvent>
  </PropertyGroup>
  <PropertyGroup>
    <PreBuildEvent>$(ProjectDir)..\Tools\nuget install $(ProjectDir)packages.config -o $(ProjectDir)..\Packages
</PreBuildEvent>
  </PropertyGroup>
</Project>
";

            // Act
            var result = VsHelper.GetTargetFrameworkContents(CsprojFileContent);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("<Project Sdk=\"Microsoft.NET.Sdk.Web\">", "Microsoft.NET.Sdk.Web")]
        [InlineData("<Project Sdk=\"Microsoft.NET.Sdk\">", "Microsoft.NET.Sdk")]
        [InlineData("parseerror<Project Sdk=\"Microsoft.NET.Sdk\">", "")]
        [InlineData("<notfoundProject Sdk=\"Microsoft.NET.Sdk\">", "")]
        [InlineData("<Project notfoundSdk=\"Microsoft.NET.Sdk\">", "")]
        public void GetGetProjectSDKContent(string content, string expected)
        {
            // Arrange
            var CsprojFileContent = string.Format(
@"{0}

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

</Project>
", content);

            // Act
            var result = VsHelper.GetProjectSDKContent(CsprojFileContent);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{\"sdk\": {\"version\": \"2.2\"}}", "2.2")]
        [InlineData("{\"sasdasddk\": {\"version\": \"2.2\"}}", "")]
        [InlineData("abc{\"sdk\": {\"version\": \"2.2\"}}", "")]
        public void SniffGlobalJson(string globalJsonContent, string expected)
        {
            // Act
            var result = VsHelper.SniffGlobalJsonContents(globalJsonContent);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />", true)]
        [InlineData("<abcFrameworkReference Include=\"Microsoft.AspNetCore.App\" />", false)]
        [InlineData("<FrameworkReference Include=\"abcMicrosoft.AspNetCore.App\" />", false)]
        public void IncludesAnyFrameworkReferenceTest(string content, bool expected)
        {
            // Arrange
            var CsprojFileContent = string.Format(
@"<Project Sdk=""Microsoft.NET.Sdk"">
 
  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    {0}
  </ItemGroup>
</Project>
", content);

            // Act
            var result = VsHelper.IncludesAnyFrameworkReferenceContent(CsprojFileContent, "Microsoft.AspNetCore.App");

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
