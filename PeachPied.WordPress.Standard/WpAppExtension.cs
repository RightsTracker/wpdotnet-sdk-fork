﻿#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Pchp.Core;

namespace PeachPied.WordPress.Standard
{
    /// <summary>Delegate for "the_content" filter.</summary>
    public delegate string the_content_filter(string content);

    /// <summary>Delegate for "the_title" filter.</summary>
    public delegate string the_title_filter(string title, int post_id);

    /// <summary>Delegate for "the_permalink" filter.</summary>
    public delegate string the_permalink_filter(string link, int post_id);

    /// <summary>Delegate to handle shortcode, used in "add_shortcode" function.</summary>
    /// <param name="attrs">Given dictionary of attributes used in shortcode.</param>
    /// <param name="content">Content of the shortcode if any.</param>
    /// <returns>Text to be inserted instead of the shortcode in post.</returns>
    public delegate string shortcode_handler(System.Collections.IDictionary attrs, string content);

    /// <summary>
    /// Provides extension functions to <see cref="WpApp"/> instances.
    /// </summary>
    [PhpHidden]
    public static class WpAppExtension
    {
        /// <summary>
        /// Gets WordPress version string.
        /// </summary>
        public static string GetVersion(this WpApp app) => app.Context.Globals["wp_version"].ToString();

        /// <summary>
        /// Gets site url value.
        /// </summary>
        public static string GetSiteUrl(this WpApp app, string path = "", string? scheme = null) => app.Context.Call("site_url", path, scheme).ToString();

        /// <summary>
        /// Registers <c>the_content</c> filter.
        /// </summary>
        public static void FilterContent(this WpApp app, the_content_filter filter) => app.AddFilter("the_content", filter);

        /// <summary>
        /// Registers <c>the_title</c> filter.
        /// </summary>
        public static void FilterTitle(this WpApp app, the_title_filter filter) => app.AddFilter("the_title", filter);

        /// <summary>
        /// Registers <c>the_permalink</c> filter.
        /// </summary>
        public static void FilterPermalink(this WpApp app, the_permalink_filter filter) => app.AddFilter("the_permalink", filter);

        /// <summary>
        /// Hooks onto <c>rightnow_end</c> action to provide additional content to the dashboard summary box.
        /// </summary>
        public static void DashboardRightNow(this WpApp app, Action<TextWriter> htmlwriter)
        {
            app.AddFilter(
                "rightnow_end",
                new Action<Context>(ctx => htmlwriter(ctx.Output)));
        }

        /// <summary>
        /// Hooks onto <c>wp_dashboard_setup</c> and registers <c>dashboard_widget</c> through <c>wp_add_dashboard_widget</c> API call.
        /// </summary>
        public static void DashboardWidget(this WpApp app, string widget_id, string widget_name, Action<TextWriter> htmlwriter)
        {
            app.AddFilter(
                "wp_dashboard_setup",
                new Action(() => app.Context.Call("wp_add_dashboard_widget",
                    (PhpValue)widget_id, (PhpValue)widget_name, PhpValue.FromClass(new Action(() => htmlwriter(app.Context.Output))))));
        }

        /// <summary>
        /// Adds meta data to a user.
        /// </summary>
        public static bool AddUserMeta(this WpApp app, int userid, string metakey, PhpValue metavalue, bool unique = false)
        {
            return app.Context
                .Call("add_user_meta", (PhpValue)userid, (PhpValue)metakey, metavalue, unique)
                .IsInteger();
        }

        /// <summary>
        /// Retrieve user meta field for a user.
        /// </summary>
        /// <returns>
        /// Will be an array if <paramref name="single"/> is <c>false</c>.
        /// Will be value of meta data field if <paramref name="single"/> is <c>true</c>.
        /// </returns>
        public static PhpValue GetUserMeta(this WpApp app, int userId, string metaKey, bool single = false)
            => GetMetaData(app, "user", userId, metaKey, single);

        /// <summary>
        /// Retrieve metadata for the specified object.
        /// </summary>
        /// <returns>
        /// Will be an array if <paramref name="single"/> is <c>false</c>.
        /// Will be value of meta data field if <paramref name="single"/> is <c>true</c>.
        /// </returns>
        public static PhpValue GetMetaData(this WpApp app, string metaType, int objectId, string metaKey, bool single = false)
        {
            return app.Context.Call("get_metadata", metaType, (PhpValue)objectId, metaKey, single);
        }

        /// <summary>
        /// Retrieves an option value based on an option name.
        /// </summary>
        public static PhpValue GetOption(this WpApp app, string option, PhpValue @default = default/*NULL*/)
        {
            return app.Context.Call("get_option", option, @default);
        }

        /// <summary>
        /// Updates the value of an option that was already added.
        /// </summary>
        public static bool UpdateOption(this WpApp app, string option, PhpValue value)
        {
            return (bool)app.Context.Call("update_option", option, value);
        }

        /// <summary>
        /// Gets <c>admin_email</c> option (Administrator email).
        /// </summary>
        public static string? GetAdminEmail(this WpApp app)
        {
            return GetOption(app, "admin_email").IsString(out var email) ? email : null;
        }

        /// <summary>
        /// Registers ajax hook.
        /// </summary>
        /// <param name="app">WP app.</param>
        /// <param name="action">Name of the ajax action. Internally it will be prefixed with "wp_ajax_{<paramref name="action"/>}".</param>
        /// <param name="callback">The callback handler.</param>
        public static void AddAjaxAction(this WpApp app, string action, Func<string> callback)
        {
            app.AddFilter("wp_ajax_" + action, new Action(() =>
            {
                app.Context.Echo(callback());
                app.Context.Call("wp_die");
            }));
        }

        /// <summary>
        /// Enqueues .js script.
        /// </summary>
        /// <param name="app">WP app.</param>
        /// <param name="handle">Name of the script. Should be unique.</param>
        /// <param name="src">
        /// Full URL of the script, or path of the script relative to the WordPress root directory.
        /// Default: ''
        /// </param>
        /// <param name="inFooter">Enqueue the script to footer instead of header.</param>
        public static void EnqueueScript(this WpApp app, string handle, string src, bool inFooter = false)
        {
            // wp_enqueue_script( string $handle, string $src = '', string[] $deps = array(), string|bool|null $ver = false, bool $in_footer = false )
            app.Context.Call("wp_enqueue_script", handle, src, PhpArray.NewEmpty(), PhpValue.False, inFooter);
        }

        /// <summary>
        /// <c>wp_enqueue_scripts</c> hook.
        /// </summary>
        public static void EnqueueScripts(this WpApp app, Action callback)
        {
            app.AddFilter("wp_enqueue_scripts", callback);
        }

        /// <summary>
        /// Enqueues .js script.
        /// </summary>
        /// <param name="app">WP app.</param>
        /// <param name="handle">Name of the script. Should be unique.</param>
        /// <param name="src">
        /// Full URL of the script, or path of the script relative to the WordPress root directory.
        /// Default: ''
        /// </param>
        public static void EnqueueStyle(this WpApp app, string handle, string src)
        {
            // wp_enqueue_style( 'style-name', get_stylesheet_uri() );
            app.Context.Call("wp_enqueue_style", handle, src);
        }

        /// <summary>
        /// Registers 'admin_init' hook.
        /// </summary>
        public static void OnAdminInit(this WpApp app, Action action) => app.AddFilter("admin_init", action);

        /// <summary>
        /// Registers 'admin_menu' hook.
        /// </summary>
        public static void AdminMenu(this WpApp app, Action action) => app.AddFilter("admin_menu", action);

        /// <summary>
        /// Registers 'admin_notices' callback.
        /// </summary>
        /// <param name="app">WP app.</param>
        /// <param name="callback">Delegate that returns the notices HTML code.</param>
        public static void AdminNotices(this WpApp app, Func<string> callback)
        {
            app.AddFilter("admin_notices", new Action(() => app.Context.Echo(callback())));
        }

        /// <summary>
        /// Registers 'add_management_page' callback.
        /// </summary>
        public static string? AddManagementPage(this WpApp app, string pageTitle, string menuTitle, string capability, string slug, Action<TextWriter> callback, int? position = null)
        {
            var hook = app.Context.Call("add_management_page", pageTitle, menuTitle, capability, slug, new Action(() =>
            {
                callback(app.Context.Output);

            }), position.HasValue ? position.Value : PhpValue.Null);

            if (hook.IsFalse)
            {
                return null;
            }

            return hook.ToString();
        }

        /// <summary>
        /// Registers 'wp_footer' callback.
        /// </summary>
        public static void Footer(this WpApp app, Action<TextWriter> callback, long priority = 100)
        {
            app.AddFilter("wp_footer", new Action(() =>
            {
                callback(app.Context.Output);
            }), priority);
        }

        /// <summary>
        /// Registers 'admin_footer_text' callback.
        /// </summary>
        public static void AdminFooterText(this WpApp app, Action<TextWriter> callback, long priority = 100)
        {
            app.AddFilter("admin_footer_text", new Action(() =>
            {
                callback(app.Context.Output);
            }), priority);
        }
    }
}
