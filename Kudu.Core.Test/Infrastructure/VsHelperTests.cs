using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class VsHelperFacts
    {
        [Fact]
        public void IsDotNetCore3DetectsDotNetCore3_1()
        {
            // Arrange
            var CsprojFileContent =
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Content Include=""run.bat"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
";

            // Act
            var result = VsHelper.IsDotNetCore3CsProj(CsprojFileContent);

            // Assert
            Assert.Equal(result, true);
        }

        [Fact]
        public void IsDotNetCore3DetectsDotNetCore3_0()
        {
            // Arrange
            var CsprojFileContent =
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <Content Include=""run.bat"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
";

            // Act
            var result = VsHelper.IsDotNetCore3CsProj(CsprojFileContent);

            // Assert
            Assert.Equal(result, true);
        }

        [Fact]
        public void IsDotNetCore3DetectsDotNetCore2_2()
        {
            // Arrange
            var CsprojFileContent =
@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.2</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <Content Include=""run.bat"">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
";

            // Act
            var result = VsHelper.IsDotNetCore3CsProj(CsprojFileContent);

            // Assert
            Assert.Equal(result, false);
        }

        [Fact]
        public void IsDotNetCore3DetectsDotNetFramework()
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
            var result = VsHelper.IsDotNetCore3CsProj(CsprojFileContent);

            // Assert
            Assert.Equal(result, false);
        }

        [Fact]
        public void IsDotNetCore3DetectsOldStyle()
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
            var result = VsHelper.IsDotNetCore3CsProj(CsprojFileContent);

            // Assert
            Assert.Equal(result, false);
        }
    }
}
