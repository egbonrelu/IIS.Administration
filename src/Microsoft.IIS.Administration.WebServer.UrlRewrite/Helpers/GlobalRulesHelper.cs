﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.WebServer.UrlRewrite
{
    using Microsoft.IIS.Administration.Core;
    using Microsoft.IIS.Administration.Core.Utils;
    using Microsoft.Web.Administration;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;

    static class GlobalRulesHelper
    {
        public static readonly Fields SectionRefFields = new Fields("id", "scope");
        public static readonly Fields RuleRefFields = new Fields("name", "id");

        public static string GetSectionLocation(string id)
        {
            return $"/{Defines.GLOBAL_RULES_SECTION_PATH}/{id}";
        }

        public static string GetRuleLocation(string id)
        {
            return $"/{Defines.GLOBAL_RULES_PATH}/{id}";
        }

        public static InboundRulesSection GetSection(Site site, string path, string configPath = null)
        {
            return (InboundRulesSection)ManagementUnit.GetConfigSection(site?.Id,
                                                                           path,
                                                                           Globals.GlobalRulesSectionName,
                                                                           typeof(InboundRulesSection),
                                                                           configPath);
        }

        public static bool IsSectionLocal(Site site, string path)
        {
            return ManagementUnit.IsSectionLocal(site?.Id,
                                                 path,
                                                 Globals.GlobalRulesSectionName);
        }

        public static object SectionToJsonModelRef(Site site, string path, Fields fields = null)
        {
            if (fields == null || !fields.HasFields) {
                return SectionToJsonModel(site, path, SectionRefFields, false);
            }
            else {
                return SectionToJsonModel(site, path, fields, false);
            }
        }

        public static object SectionToJsonModel(Site site, string path, Fields fields = null, bool full = true)
        {
            if (fields == null) {
                fields = Fields.All;
            }

            RewriteId id = new RewriteId(site?.Id, path);
            var section = GetSection(site, path);

            dynamic obj = new ExpandoObject();

            //
            // id
            if (fields.Exists("id")) {
                obj.id = id.Uuid;
            }

            //
            // scope
            if (fields.Exists("scope")) {
                obj.scope = site == null ? string.Empty : site.Name + path;
            }

            //
            // use_original_url_encoding
            if (fields.Exists("use_original_url_encoding") && section.Schema.HasAttribute(InboundRulesSection.UseOriginalUrlEncodingAttribute)) {
                obj.use_original_url_encoding = section.UseOriginalURLEncoding;
            }

            //
            // metadata
            if (fields.Exists("metadata")) {
                obj.metadata = ConfigurationUtility.MetadataToJson(section.IsLocallyStored, section.IsLocked, section.OverrideMode, section.OverrideModeEffective);
            }

            //
            // url_rewrite
            if (fields.Exists("url_rewrite")) {
                obj.url_rewrite = RewriteHelper.ToJsonModelRef(site, path);
            }

            return Core.Environment.Hal.Apply(Defines.GlobalRulesSectionResource.Guid, obj, full);
        }

        public static object RuleToJsonModelRef(InboundRule rule, Site site, string path, Fields fields = null)
        {
            if (fields == null || !fields.HasFields) {
                return RuleToJsonModel(rule, site, path, RuleRefFields, false);
            }
            else {
                return RuleToJsonModel(rule, site, path, fields, false);
            }
        }

        public static object RuleToJsonModel(InboundRule rule, Site site, string path, Fields fields = null, bool full = true)
        {
            if (rule == null) {
                return null;
            }

            if (fields == null) {
                fields = Fields.All;
            }

            var globalRuleId = new InboundRuleId(site?.Id, path, rule.Name);

            dynamic obj = new ExpandoObject();

            //
            // name
            if (fields.Exists("name")) {
                obj.name = rule.Name;
            }

            //
            // id
            if (fields.Exists("id")) {
                obj.id = globalRuleId.Uuid;
            }

            //
            // pattern
            if (fields.Exists("pattern")) {
                obj.pattern = rule.Match.Pattern;
            }

            //
            // pattern_syntax
            if (fields.Exists("pattern_syntax")) {
                obj.pattern_syntax = PatternSyntaxHelper.ToJsonModel(rule.PatternSyntax);
            }

            //
            // ignore_case
            if (fields.Exists("ignore_case")) {
                obj.ignore_case = rule.Match.IgnoreCase;
            }

            //
            // negate
            if (fields.Exists("negate")) {
                obj.negate = rule.Match.Negate;
            }

            //
            // stop_processing
            if (fields.Exists("stop_processing")) {
                obj.stop_processing = rule.StopProcessing;
            }

            //
            // response_cache_directive
            if (fields.Exists("response_cache_directive") && rule.Schema.HasAttribute(InboundRule.ResponseCacheDirectiveAttribute)) {
                obj.response_cache_directive = ResponseCacheDirectiveHelper.ToJsonModel(rule.ResponseCacheDirective);
            }

            //
            // condition_match_constraints
            if (fields.Exists("condition_match_constraints")) {
                obj.condition_match_constraints = LogicalGroupingHelper.ToJsonModel(rule.Conditions.LogicalGrouping);
            }

            //
            // track_all_captures
            if (fields.Exists("track_all_captures")) {
                obj.track_all_captures = rule.Conditions.TrackAllCaptures;
            }

            //
            // action
            if (fields.Exists("action")) {
                obj.action = new ExpandoObject();
                dynamic action = obj.action;

                action.type = ActionTypeHelper.ToJsonModel(rule.Action.Type);
                action.url = rule.Action.Url;
                action.append_query_string = rule.Action.AppendQueryString;

                if (rule.Action.Type == ActionType.Redirect) {
                    action.redirect_type = Enum.GetName(typeof(RedirectType), rule.Action.RedirectType).ToLowerInvariant();
                }

                if (rule.Action.Type == ActionType.CustomResponse) {
                    action.status_code = rule.Action.StatusCode;
                    action.sub_status_code = rule.Action.SubStatusCode;
                    action.description = rule.Action.StatusDescription;
                    action.reason = rule.Action.StatusReason;
                }
            }

            //
            // server_variables
            if (fields.Exists("server_variables")) {
                obj.server_variables = rule.ServerVariableAssignments.Select(s => new {
                    name = s.Name,
                    value = s.Value,
                    replace = s.Replace
                });
            }

            //
            // conditions
            if (fields.Exists("conditions")) {
                obj.conditions = rule.Conditions.Select(c => new {
                    input = c.Input,
                    pattern = c.Pattern,
                    negate = c.Negate,
                    ignore_case = c.IgnoreCase,
                    match_type = MatchTypeHelper.ToJsonModel(c.MatchType)
                });
            }

            //
            // url_rewrite
            if (fields.Exists("url_rewrite")) {
                obj.url_rewrite = RewriteHelper.ToJsonModelRef(site, path, fields.Filter("url_rewrite"));
            }

            return Core.Environment.Hal.Apply(Defines.GlobalRulesResource.Guid, obj);
        }

        public static void UpdateSection(dynamic model, Site site, string path, string configPath = null)
        {
            if (model == null) {
                throw new ApiArgumentException("model");
            }

            InboundRulesSection section = GetSection(site, path, configPath);

            try {
                // UseOriginalURLEncoding introduced in 2.1
                if (section.Schema.HasAttribute(InboundRulesSection.UseOriginalUrlEncodingAttribute)) {
                    DynamicHelper.If<bool>((object)model.use_original_url_encoding, v => section.UseOriginalURLEncoding = v);
                }

                if (model.metadata != null) {
                    DynamicHelper.If<OverrideMode>((object)model.metadata.override_mode, v => {
                        section.OverrideMode = v;
                    });
                }
            }
            catch (FileLoadException e) {
                throw new LockedException(section.SectionPath, e);
            }
            catch (DirectoryNotFoundException e) {
                throw new ConfigScopeNotFoundException(e);
            }
        }

        public static void UpdateRule(dynamic model, InboundRule rule, InboundRulesSection section)
        {
            SetRule(model, rule, section);
        }

        public static InboundRule CreateRule(dynamic model, InboundRulesSection section)
        {
            if (model == null) {
                throw new ApiArgumentException("model");
            }

            if (string.IsNullOrEmpty(DynamicHelper.Value(model.name))) {
                throw new ApiArgumentException("name");
            }

            if (string.IsNullOrEmpty(DynamicHelper.Value(model.pattern))) {
                throw new ApiArgumentException("pattern");
            }

            if (string.IsNullOrEmpty(DynamicHelper.Value(model.pattern_syntax))) {
                throw new ApiArgumentException("pattern_syntax");
            }

            if (model.action == null) {
                throw new ApiArgumentException("action");
            }

            if (!(model.action is JObject)) {
                throw new ApiArgumentException("action", ApiArgumentException.EXPECTED_OBJECT);
            }

            if (string.IsNullOrEmpty(DynamicHelper.Value(model.action.type))) {
                throw new ApiArgumentException("action.type");
            }

            if (string.IsNullOrEmpty(DynamicHelper.Value(model.action.url))) {
                throw new ApiArgumentException("action.url");
            }

            var rule = (InboundRule)section.InboundRules.CreateElement();

            SetRule(model, rule, section);

            return rule;
        }

        public static void AddRule(InboundRule rule, InboundRulesSection section)
        {
            if (rule == null) {
                throw new ArgumentNullException(nameof(rule));
            }

            if (rule.Name == null) {
                throw new ArgumentNullException("rule.Name");
            }

            InboundRuleCollection collection = section.InboundRules;

            if (collection.Any(r => r.Name.Equals(rule.Name))) {
                throw new AlreadyExistsException("rule");
            }

            try {
                collection.Add(rule);
            }
            catch (FileLoadException e) {
                throw new LockedException(section.SectionPath, e);
            }
            catch (DirectoryNotFoundException e) {
                throw new ConfigScopeNotFoundException(e);
            }
        }

        public static void DeleteRule(InboundRule rule, InboundRulesSection section)
        {
            if (rule == null) {
                return;
            }

            InboundRuleCollection collection = section.InboundRules;

            // To utilize the remove functionality we must pull the element directly from the collection
            rule = (InboundRule)collection.FirstOrDefault(r => r.Name.Equals(rule.Name));

            if (rule != null) {
                try {
                    collection.Remove(rule);
                }
                catch (FileLoadException e) {
                    throw new LockedException(section.SectionPath, e);
                }
                catch (DirectoryNotFoundException e) {
                    throw new ConfigScopeNotFoundException(e);
                }
            }
        }

        private static void SetRule(dynamic model, InboundRule rule, InboundRulesSection section)
        {
            if (model == null) {
                throw new ApiArgumentException("model");
            }

            //
            // Name, check for already existing name
            string name = DynamicHelper.Value(model.name);
            if (!string.IsNullOrEmpty(name)) {
                if (!name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase) &&
                        section.InboundRules.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) {
                    throw new AlreadyExistsException("name");
                }

                rule.Name = name;
            }

            DynamicHelper.If((object)model.pattern, v => rule.Match.Pattern = v);
            DynamicHelper.If<bool>((object)model.ignore_case, v => rule.Match.IgnoreCase = v);
            DynamicHelper.If<bool>((object)model.negate, v => rule.Match.Negate = v);
            DynamicHelper.If<bool>((object)model.stop_processing, v => rule.StopProcessing = v);
            DynamicHelper.If((object)model.pattern_syntax, v => rule.PatternSyntax = PatternSyntaxHelper.FromJsonModel(v));

            //
            // Action
            dynamic action = model.action;
            if (action != null) {

                if (!(action is JObject)) {
                    throw new ApiArgumentException("action", ApiArgumentException.EXPECTED_OBJECT);
                }

                DynamicHelper.If((object)action.type, v => rule.Action.Type = ActionTypeHelper.FromJsonModel(v));
                DynamicHelper.If((object)action.url, v => rule.Action.Url = v);
                DynamicHelper.If<bool>((object)action.append_query_string, v => rule.Action.AppendQueryString = v);
                DynamicHelper.If<long>((object)action.status_code, v => rule.Action.StatusCode = v);
                DynamicHelper.If<long>((object)action.sub_status_code, v => rule.Action.SubStatusCode = v);
                DynamicHelper.If((object)action.description, v => rule.Action.StatusDescription = v);
                DynamicHelper.If((object)action.reason, v => rule.Action.StatusReason = v);
                DynamicHelper.If<RedirectType>((object)action.redirect_type, v => rule.Action.RedirectType = v);
            }

            //
            // Server variables
            if (model.server_variables != null) {

                IEnumerable<dynamic> serverVariables = model.server_variables as IEnumerable<dynamic>;

                if (serverVariables == null) {
                    throw new ApiArgumentException("server_variables", ApiArgumentException.EXPECTED_ARRAY);
                }

                rule.ServerVariableAssignments.Clear();

                foreach (dynamic serverVariable in serverVariables) {
                    if (!(serverVariable is JObject)) {
                        throw new ApiArgumentException("server_variables.item");
                    }

                    string svName = DynamicHelper.Value(serverVariable.name);
                    string svValue = DynamicHelper.Value(serverVariable.value);
                    bool svReplace = DynamicHelper.To<bool>(serverVariable.replace) ?? false;

                    if (string.IsNullOrEmpty(svName)) {
                        throw new ApiArgumentException("server_variables.item.name", "Required");
                    }

                    if (string.IsNullOrEmpty(svValue)) {
                        throw new ApiArgumentException("server_variables.item.value", "Required");
                    }

                    var svAssignment = rule.ServerVariableAssignments.CreateElement();
                    svAssignment.Name = svName;
                    svAssignment.Value = svValue;
                    svAssignment.Replace = svReplace;

                    rule.ServerVariableAssignments.Add(svAssignment);
                }
            }

            DynamicHelper.If((object)model.condition_match_constraints, v => rule.Conditions.LogicalGrouping = LogicalGroupingHelper.FromJsonModel(v));
            DynamicHelper.If<bool>((object)model.track_all_captures, v => rule.Conditions.TrackAllCaptures = v);

            //
            // Conditions
            if (model.conditions != null) {

                IEnumerable<dynamic> conditions = model.conditions as IEnumerable<dynamic>;

                if (conditions == null) {
                    throw new ApiArgumentException("conditions", ApiArgumentException.EXPECTED_ARRAY);
                }

                rule.Conditions.Clear();

                foreach (dynamic condition in conditions) {
                    if (!(condition is JObject)) {
                        throw new ApiArgumentException("conditions.item");
                    }

                    string input = DynamicHelper.Value(condition.input);

                    if (string.IsNullOrEmpty(input)) {
                        throw new ApiArgumentException("conditions.item.input", "Required");
                    }

                    var con = rule.Conditions.CreateElement();
                    con.Input = input;
                    //
                    // Only pattern match type allowed in schema
                    con.MatchType = MatchType.Pattern;
                    con.Pattern = DynamicHelper.Value(condition.pattern);
                    con.Negate = DynamicHelper.To<bool>(condition.negate);
                    con.IgnoreCase = DynamicHelper.To<bool>(condition.ignore_case);

                    rule.Conditions.Add(con);
                }
            }

            if (rule.Schema.HasAttribute(InboundRule.ResponseCacheDirectiveAttribute)) {
                DynamicHelper.If((object)model.response_cache_directive, v => rule.ResponseCacheDirective = ResponseCacheDirectiveHelper.FromJsonModel(v));
            }
        }
    }
}
