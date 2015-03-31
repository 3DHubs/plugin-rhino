using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rhino.PlugIns;


// Plug-in Description Attributes - all of these are optional
// These will show in Rhino's option dialog, in the tab Plug-ins
[assembly: PlugInDescription(DescriptionType.Address, "-")]
[assembly: PlugInDescription(DescriptionType.Country, "-")]
[assembly: PlugInDescription(DescriptionType.Email, "-")]
[assembly: PlugInDescription(DescriptionType.Phone, "-")]
[assembly: PlugInDescription(DescriptionType.Fax, "-")]
[assembly: PlugInDescription(DescriptionType.Organization, "3D Hubs")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "http://www.3dhubs.com/")]
[assembly: PlugInDescription(DescriptionType.WebSite, "http://www.3dhubs.com/")]


// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("3D Hubs")] // Plug-In title is extracted from this
[assembly: AssemblyDescription("3D Hubs Model Uploader")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("3D Hubs")]
[assembly: AssemblyProduct("3D Hubs")]
[assembly: AssemblyCopyright("Copyright © 3D Hubs 2015")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyInformationalVersion("2")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("E803BCAA-11E5-4948-AE54-2187EF717949")] // This will also be the Guid of the Rhino plug-in

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
