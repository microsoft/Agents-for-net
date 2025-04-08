﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Agents.Builder.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Agents.Builder.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The method does not match the RouteHandler delegate definition..
        /// </summary>
        internal static string AttributeHandlerInvalid {
            get {
                return ResourceManager.GetString("AttributeHandlerInvalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A RouteAttribute is missing required arguments..
        /// </summary>
        internal static string AttributeMissingArgs {
            get {
                return ResourceManager.GetString("AttributeMissingArgs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The RouteAttribute.Selector method &apos;{0}&apos; does not match the RouteSelector delegate definition..
        /// </summary>
        internal static string AttributeSelectorInvalid {
            get {
                return ResourceManager.GetString("AttributeSelectorInvalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The RouteAttribute.Selector method &apos;{0}&apos; is not found..
        /// </summary>
        internal static string AttributeSelectorNotFound {
            get {
                return ResourceManager.GetString("AttributeSelectorNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to create user Authorization provider for handler name &apos;{0}&apos;.
        /// </summary>
        internal static string FailedToCreateUserAuthorizationHandler {
            get {
                return ResourceManager.GetString("FailedToCreateUserAuthorizationHandler", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An instance of IAccessTokenProvider not found for {0}.
        /// </summary>
        internal static string IAccessTokenProviderNotFound {
            get {
                return ResourceManager.GetString("IAccessTokenProviderNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No UserAuthorization Handlers were defined..
        /// </summary>
        internal static string NoUserAuthorizationHandlers {
            get {
                return ResourceManager.GetString("NoUserAuthorizationHandlers", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OBO exchange failed for connection &apos;{0}&apos; with scopes {1}..
        /// </summary>
        internal static string OBOExchangeFailed {
            get {
                return ResourceManager.GetString("OBOExchangeFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OBO for &apos;{0}&apos; cannot exchange an application that does not have an api:// audience..
        /// </summary>
        internal static string OBONotExchangeableToken {
            get {
                return ResourceManager.GetString("OBONotExchangeableToken", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to OBO not supported on &apos;{0}&apos;.
        /// </summary>
        internal static string OBONotSupported {
            get {
                return ResourceManager.GetString("OBONotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to StreamingResponse instance has already ended..
        /// </summary>
        internal static string StreamingResponseEnded {
            get {
                return ResourceManager.GetString("StreamingResponseEnded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to StreamingResponse in Teams requires QueueInformativeUpdate to be called first..
        /// </summary>
        internal static string TeamsRequiresInformativeFirst {
            get {
                return ResourceManager.GetString("TeamsRequiresInformativeFirst", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AgentApplication.Authorization requires AgentApplicationOptions.Adapter set..
        /// </summary>
        internal static string UserAuthenticationRequiresAdapter {
            get {
                return ResourceManager.GetString("UserAuthenticationRequiresAdapter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UserAuthorization sign in for &apos;{0}&apos; is already in progress..
        /// </summary>
        internal static string UserAuthorizationAlreadyActive {
            get {
                return ResourceManager.GetString("UserAuthorizationAlreadyActive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Handler name &apos;{0}&apos; not found in configuration under AgentApplication:UserAuthorization:Handlers, or AgentApplication:UserAuthorization:DefaultHandlerName is invalid..
        /// </summary>
        internal static string UserAuthorizationDefaultHandlerNotFound {
            get {
                return ResourceManager.GetString("UserAuthorizationDefaultHandlerNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error occurred while trying to authenticate user with &apos;{0}&apos;.
        /// </summary>
        internal static string UserAuthorizationFailed {
            get {
                return ResourceManager.GetString("UserAuthorizationFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Handler name &apos;{0}&apos; not found in configuration under AgentApplication:UserAuthorization:Handlers, or AgentApplication:UserAuthorization:DefaultHandlerName is invalid..
        /// </summary>
        internal static string UserAuthorizationHandlerNotFound {
            get {
                return ResourceManager.GetString("UserAuthorizationHandlerNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The AgentApplication.Authorization feature is unavailable because no user Authorization handlers were configured in AgentApplication:UserAuthorization:Handlers..
        /// </summary>
        internal static string UserAuthorizationNotConfigured {
            get {
                return ResourceManager.GetString("UserAuthorizationNotConfigured", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AgentApplication.UserAuthorization requires AgentApplicationOptions.Adapter set..
        /// </summary>
        internal static string UserAuthorizationRequiresAdapter {
            get {
                return ResourceManager.GetString("UserAuthorizationRequiresAdapter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type &apos;{0}&apos; not found in Assembly &apos;{1}&apos; or is the wrong type for &apos;{2}&apos;..
        /// </summary>
        internal static string UserAuthorizationTypeNotFound {
            get {
                return ResourceManager.GetString("UserAuthorizationTypeNotFound", resourceCulture);
            }
        }
    }
}
