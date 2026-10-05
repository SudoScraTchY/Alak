using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;

namespace Alak.App.ViewModels;

public partial class RulesViewModel : ObservableObject
{
    private readonly IUserRulesRepository _rulesRepo;

    // Static helpers for picker ItemsSource
    public static IEnumerable<Category> CategoryValues => Enum.GetValues<Category>();
    public static IEnumerable<UserRuleActionType> RuleActionValues => Enum.GetValues<UserRuleActionType>();
    public static IEnumerable<RuleMatchMode> RuleMatchModeValues => Enum.GetValues<RuleMatchMode>();

    [ObservableProperty]
    private ObservableCollection<UserRuleViewModel> _rules = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _newRulePattern = "";

    [ObservableProperty]
    private Category _newRuleCategory = Category.Offers;

    [ObservableProperty]
    private UserRuleActionType _newRuleAction = UserRuleActionType.Allow;

    [ObservableProperty]
    private RuleMatchMode _newRuleMatchMode = RuleMatchMode.Contains;

    [ObservableProperty]
    private int _newRulePriority = 0;

    [ObservableProperty]
    private bool _newRuleCaseSensitive = false;

    public ICommand AddRuleCommand { get; }
    public ICommand DeleteRuleCommand { get; }
    public ICommand ToggleRuleCommand { get; }
    public ICommand ReorderRulesCommand { get; }
    public ICommand LoadRulesCommand { get; }

    public RulesViewModel(IUserRulesRepository rulesRepo)
    {
        _rulesRepo = rulesRepo;

        AddRuleCommand = new AsyncRelayCommand(AddRuleAsync, () => !string.IsNullOrWhiteSpace(NewRulePattern));
        DeleteRuleCommand = new AsyncRelayCommand<UserRuleViewModel>(DeleteRuleAsync);
        ToggleRuleCommand = new AsyncRelayCommand<UserRuleViewModel>(ToggleRuleAsync);
        ReorderRulesCommand = new AsyncRelayCommand(ReorderRulesAsync);
        LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync);

        _ = LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        IsLoading = true;
        try
        {
            var rules = await _rulesRepo.GetAllRulesAsync();
            Rules.Clear();
            foreach (var rule in rules.OrderBy(r => r.Priority).ThenBy(r => r.Id))
            {
                Rules.Add(new UserRuleViewModel(rule));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRulePattern)) return;

        var match = new UserRuleMatch
        {
            TextContains = NewRulePattern.Trim()
        };

        var action = new UserRuleAction
        {
            SetCategory = NewRuleCategory.ToKebabId(),
            Mute = NewRuleAction == UserRuleActionType.Mute,
            ArchiveImmediately = NewRuleAction == UserRuleActionType.Archive,
            AddTags = NewRuleAction == UserRuleActionType.Tag ? new[] { "user-tag" } : null
        };

        var rule = new UserRule(
            Id: 0,
            Priority: NewRulePriority,
            Match: match,
            Action: action,
            Enabled: true,
            CreatedAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UpdatedAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            MatchMode = NewRuleMatchMode,
            CaseSensitive = NewRuleCaseSensitive
        };

        var id = await _rulesRepo.InsertRuleAsync(rule);
        rule = rule with { Id = id };
        Rules.Add(new UserRuleViewModel(rule));

        // Reset form
        NewRulePattern = "";
        NewRulePriority = 0;
    }

    private async Task DeleteRuleAsync(UserRuleViewModel? ruleVm)
    {
        if (ruleVm == null) return;
        await _rulesRepo.DeleteRuleAsync(ruleVm.Rule.Id);
        Rules.Remove(ruleVm);
    }

    private async Task ToggleRuleAsync(UserRuleViewModel? ruleVm)
    {
        if (ruleVm == null) return;
        var rule = ruleVm.Rule with { Enabled = !ruleVm.Rule.Enabled, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        await _rulesRepo.UpdateRuleAsync(rule);
        ruleVm.Rule = rule;
        ruleVm.Refresh();
    }

    private async Task ReorderRulesAsync()
    {
        var orderedIds = Rules.Select(r => r.Rule.Id).ToList();
        await _rulesRepo.ReorderRulesAsync(orderedIds);
    }
}

public partial class UserRuleViewModel : ObservableObject
{
    private UserRule _rule;

    public UserRuleViewModel(UserRule rule)
    {
        _rule = rule;
    }

    public UserRule Rule 
    { 
        get => _rule; 
        set => SetProperty(ref _rule, value); 
    }

    public string Pattern => Rule.Match.TextContains ?? "";
    public string Category => Rule.Action.SetCategory ?? "other";
    public string ActionType => Rule.ActionType.ToString();
    public string MatchMode => Rule.MatchMode.ToString();
    public int Priority => Rule.Priority;
    public bool Enabled => Rule.Enabled;
    public string EnabledText => Rule.Enabled ? "Enabled" : "Disabled";

    public void Refresh() => OnPropertyChanged(nameof(EnabledText));
}