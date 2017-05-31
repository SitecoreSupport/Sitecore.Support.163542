using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using Sitecore.Shell.Framework;
using Sitecore.Text;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace Sitecore.Support
{
    /// <summary>
    /// The aliases form.
    /// </summary>
    public class AliasesForm : DialogForm
    {
        /// <summary>
        /// Alias Info
        /// </summary>
        private class AliasInfo
        {
            /// <summary>
            /// The path.
            /// </summary>
            private readonly ListString _path;

            /// <summary>
            /// Gets the ascenders.
            /// </summary>
            /// <value>The ascenders.</value>
            public System.Collections.Generic.IEnumerable<string> Ascenders
            {
                get
                {
                    if (this._path.Count > 1)
                    {
                        for (int i = 0; i < this._path.Count - 1; i++)
                        {
                            yield return this._path[i];
                        }
                    }
                    yield break;
                }
            }

            /// <summary>
            /// Gets the name of the ascenders and.
            /// </summary>
            /// <value>The name of the ascenders and.</value>
            public System.Collections.Generic.IEnumerable<string> AscendersAndName
            {
                get
                {
                    return this._path.Items;
                }
            }

            /// <summary>
            /// Gets the name.
            /// </summary>
            /// <value>The name.</value>
            public string Name
            {
                get
                {
                    return this._path[this._path.Count - 1];
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.ContentManager.Dialogs.Aliases.AliasInfo" /> class.
            /// </summary>
            /// <param name="value">
            /// The value.
            /// </param>
            public AliasInfo(string value)
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                value = StringUtil.RemovePrefix("/", value);
                value = StringUtil.RemovePostfix("/", value);
                this._path = new ListString(value, '/');
            }
        }

        /// <summary>
        /// The list.
        /// </summary>
        protected Listbox List;

        /// <summary></summary>
        protected Edit NewAlias;

        /// <summary></summary>
        protected Border ListHolder;

        /// <summary>
        /// Handles a click on the Add button.
        /// </summary>
        protected void Add_Click()
        {
            string value = this.NewAlias.Value;
            if (value.Length == 0)
            {
                SheerResponse.Alert("Enter a value in the Add Input field.", new string[0]);
                return;
            }
            AliasInfo aliasInfo = new AliasInfo(value);
            foreach (string current in aliasInfo.AscendersAndName)
            {
                if (!Regex.IsMatch(current, Settings.ItemNameValidation, RegexOptions.ECMAScript))
                {
                    SheerResponse.Alert("The name contains invalid characters.", new string[0]);
                    return;
                }
                if (current.Length > Settings.MaxItemNameLength)
                {
                    SheerResponse.Alert("The name is too long.", new string[0]);
                    return;
                }
            }
            Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);
            Error.AssertItemFound(itemFromQueryString);
            Item item = Context.ContentDatabase.GetItem("/sitecore/system/Aliases");
            Error.AssertItemFound(item, "/sitecore/system/Aliases");
            ListItem listItem = this.CreateAlias(aliasInfo, itemFromQueryString, item);
            if (listItem != null)
            {
                SheerResponse.Eval(string.Concat(new string[]
				{
					"scCreateAlias(",
					StringUtil.EscapeJavascriptString(StringUtil.EscapeQuote(listItem.ID)),
					", ",
					StringUtil.EscapeJavascriptString(StringUtil.EscapeQuote(listItem.Header)),
					", ",
					StringUtil.EscapeJavascriptString(StringUtil.EscapeQuote(listItem.Value)),
					");"
				}));
                this.NewAlias.Value = string.Empty;
                SheerResponse.SetModified(false);
                return;
            }
        }

        /// <summary>
        /// Raises the load event.
        /// </summary>
        /// <param name="e">
        /// The <see cref="T:System.EventArgs" /> instance containing the event data.
        /// </param>
        protected override void OnLoad(System.EventArgs e)
        {
            Assert.CanRunApplication("Content Editor/Ribbons/Chunks/Page Urls");
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                this.RefreshList();
            }
            Context.Site.Notifications.ItemDeleted += new ItemDeletedDelegate(this.ItemDeletedNotification);
        }

        /// <summary>
        /// Handles a click on the Remove button.
        /// </summary>
        protected void Remove_Click()
        {
            Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);
            Error.AssertItemFound(itemFromQueryString);
            System.Collections.ArrayList arrayList = new System.Collections.ArrayList();
            ListItem[] selected = this.List.Selected;
            for (int i = 0; i < selected.Length; i++)
            {
                ListItem listItem = selected[i];
                string text = listItem.ID;
                text = ShortID.Decode(StringUtil.Mid(text, 1));
                Item item = itemFromQueryString.Database.GetItem(text);
                if (item != null)
                {
                    arrayList.Add(item);
                }
            }
            if (arrayList.Count == 0)
            {
                SheerResponse.Alert("Select an alias from the list.", new string[0]);
                return;
            }
            Items.Delete(arrayList.ToArray(typeof(Item)) as Item[]);
            ListString listString = new ListString();
            foreach (Item item2 in arrayList)
            {
                listString.Add("I" + item2.ID.ToShortID());
                Log.Audit(this, "Remove alias: {0}", new string[]
				{
					AuditFormatter.FormatItem(item2)
				});
            }
            base.ServerProperties["deleted"] = listString.ToString();
            SheerResponse.SetModified(false);
        }

        /// <summary>
        /// Gets the alias path.
        /// </summary>
        /// <param name="alias">
        /// The alias.
        /// </param>
        /// <returns>
        /// The alias path.
        /// </returns>
        private static string GetAliasPath(Item alias)
        {
            Assert.ArgumentNotNull(alias, "alias");
            string text = alias.Paths.GetPath("/sitecore/system/Aliases", "/", ItemPathType.Name);
            if (text.StartsWith("/", System.StringComparison.InvariantCulture))
            {
                text = text.Substring(1);
            }
            return text;
        }

        /// <summary>
        /// Creates the alias.
        /// </summary>
        /// <param name="aliasInfo">The alias info.</param>
        /// <param name="target">The target.</param>
        /// <param name="root">The root.</param>
        private ListItem CreateAlias(AliasInfo aliasInfo, Item target, Item root)
        {
            Assert.ArgumentNotNull(aliasInfo, "aliasInfo");
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(root, "root");
            TemplateItem template = root.Database.Templates["System/Alias"];
            Error.AssertTemplate(template, "Alias");
            foreach (string current in aliasInfo.Ascenders)
            {
                //root = root.Children[current];
                //if (root == null)
                //{
                //    SheerResponse.Alert(string.Format("The parent alias '{0}' does not exist.", current), new string[0]);
                //    ListItem result = null;
                //    return result;
                //}
                Item CurrentItem = root.Children[current] ?? root.Add(current, template);
                root = CurrentItem;
            }
            if (root.Children[aliasInfo.Name] != null)
            {
                SheerResponse.Alert("An alias with this name already exists.", new string[0]);
                return null;
            }
            Item item = root.Add(aliasInfo.Name, template);
            item.Editing.BeginEdit();
            item["Linked Item"] = string.Concat(new object[]
			{
				"<link linktype=\"internal\" url=\"",
				target.Paths.ContentPath,
				"\" id=\"",
				target.ID,
				"\" />"
			});
            item.Editing.EndEdit();
            ListItem listItem = new ListItem();
            this.List.Controls.Add(listItem);
            listItem.ID = "I" + item.ID.ToShortID();
            listItem.Header = GetAliasPath(item);
            listItem.Value = item.ID.ToString();
            return listItem;
        }

        /// <summary>
        /// Called when the item is deleted.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        private void ItemDeletedNotification(object sender, ItemDeletedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            string list = (base.ServerProperties["deleted"] as string) ?? string.Empty;
            ListString listString = new ListString(list);
            foreach (string current in listString)
            {
                SheerResponse.Eval("scRemoveAlias(" + StringUtil.EscapeJavascriptString(StringUtil.EscapeQuote(current)) + ")");
            }
        }

        /// <summary>
        /// Refreshes the list.
        /// </summary>
        private void RefreshList()
        {
            Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);
            Error.AssertItemFound(itemFromQueryString);
            using (new SecurityDisabler())
            {
                this.List.Controls.Clear();
                Item item = Context.ContentDatabase.GetItem("/sitecore/system/Aliases");
                Error.AssertItemFound(item, "/sitecore/system/Aliases");
                Item[] descendants = item.Axes.GetDescendants();
                for (int i = 0; i < descendants.Length; i++)
                {
                    Item item2 = descendants[i];
                    LinkField linkField = item2.Fields["linked item"];
                    if (linkField != null && linkField.TargetID == itemFromQueryString.ID)
                    {
                        ListItem listItem = new ListItem();
                        this.List.Controls.Add(listItem);
                        listItem.ID = "I" + item2.ID.ToShortID();
                        listItem.Header = GetAliasPath(item2);
                        listItem.Value = item2.ID.ToString();
                    }
                }
            }
        }
    }
}