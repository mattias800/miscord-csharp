using System.Text.RegularExpressions;

namespace Snacka.Client.Services;

/// <summary>
/// Provides emoji shortcode to emoji conversion.
/// Converts shortcodes like :+1: to 👍 and emoticons like :) to 😊 when followed by a space.
/// </summary>
public static class EmojiShortcodeService
{
    // All replacements: emoticons and shortcodes combined
    // Sorted by length descending so longer matches are checked first
    private static readonly (string pattern, string emoji)[] Replacements;

    static EmojiShortcodeService()
    {
        var all = new List<(string, string)>();

        // Text emoticons
        all.Add((":'-)", "😂"));
        all.Add((":'(", "😢"));
        all.Add((":')", "😂"));
        all.Add((":-)", "😊"));
        all.Add((":-(", "😢"));
        all.Add((":-D", "😃"));
        all.Add((":-P", "😛"));
        all.Add((":-p", "😛"));
        all.Add((":-O", "😮"));
        all.Add((":-o", "😮"));
        all.Add((";-)", "😉"));
        all.Add((":-/", "😕"));
        all.Add((":-\\", "😕"));
        all.Add((":-|", "😐"));
        all.Add(("<3", "❤️"));
        all.Add((":)", "😊"));
        all.Add((":(", "😢"));
        all.Add((":D", "😃"));
        all.Add((":P", "😛"));
        all.Add((":p", "😛"));
        all.Add((":O", "😮"));
        all.Add((":o", "😮"));
        all.Add((";)", "😉"));
        all.Add((":/", "😕"));
        all.Add((":|", "😐"));
        all.Add((":*", "😘"));
        all.Add(("xD", "😆"));
        all.Add(("XD", "😆"));

        // Shortcodes in :word: format
        all.Add((":+1:", "👍"));
        all.Add((":-1:", "👎"));
        all.Add((":thumbsup:", "👍"));
        all.Add((":thumbsdown:", "👎"));
        all.Add((":heart:", "❤️"));
        all.Add((":smile:", "😊"));
        all.Add((":grin:", "😁"));
        all.Add((":joy:", "😂"));
        all.Add((":rofl:", "🤣"));
        all.Add((":wink:", "😉"));
        all.Add((":thinking:", "🤔"));
        all.Add((":fire:", "🔥"));
        all.Add((":100:", "💯"));
        all.Add((":clap:", "👏"));
        all.Add((":pray:", "🙏"));
        all.Add((":ok:", "👌"));
        all.Add((":wave:", "👋"));
        all.Add((":eyes:", "👀"));
        all.Add((":tada:", "🎉"));
        all.Add((":party:", "🎉"));
        all.Add((":rocket:", "🚀"));
        all.Add((":star:", "⭐"));
        all.Add((":check:", "✅"));
        all.Add((":x:", "❌"));
        all.Add((":warning:", "⚠️"));
        all.Add((":question:", "❓"));
        all.Add((":exclamation:", "❗"));
        all.Add((":poop:", "💩"));
        all.Add((":skull:", "💀"));
        all.Add((":ghost:", "👻"));
        all.Add((":alien:", "👽"));
        all.Add((":robot:", "🤖"));
        all.Add((":cat:", "🐱"));
        all.Add((":dog:", "🐶"));
        all.Add((":bee:", "🐝"));
        all.Add((":butterfly:", "🦋"));
        all.Add((":sun:", "☀️"));
        all.Add((":moon:", "🌙"));
        all.Add((":cloud:", "☁️"));
        all.Add((":rain:", "🌧️"));
        all.Add((":snow:", "❄️"));
        all.Add((":coffee:", "☕"));
        all.Add((":beer:", "🍺"));
        all.Add((":pizza:", "🍕"));
        all.Add((":cake:", "🎂"));
        all.Add((":sad:", "😢"));
        all.Add((":cry:", "😢"));
        all.Add((":laugh:", "😂"));
        all.Add((":lol:", "😂"));
        all.Add((":love:", "❤️"));
        all.Add((":angry:", "😡"));
        all.Add((":rage:", "😡"));
        all.Add((":cool:", "😎"));
        all.Add((":sunglasses:", "😎"));
        all.Add((":sleepy:", "😴"));
        all.Add((":zzz:", "😴"));
        all.Add((":sick:", "🤢"));
        all.Add((":puke:", "🤮"));
        all.Add((":devil:", "😈"));
        all.Add((":angel:", "😇"));
        all.Add((":halo:", "😇"));
        all.Add((":nerd:", "🤓"));
        all.Add((":money:", "🤑"));
        all.Add((":zip:", "🤐"));
        all.Add((":shush:", "🤫"));
        all.Add((":lying:", "🤥"));
        all.Add((":hug:", "🤗"));
        all.Add((":muscle:", "💪"));
        all.Add((":fingers_crossed:", "🤞"));
        all.Add((":crossed_fingers:", "🤞"));
        all.Add((":point_up:", "☝️"));
        all.Add((":point_down:", "👇"));
        all.Add((":point_left:", "👈"));
        all.Add((":point_right:", "👉"));
        all.Add((":fist:", "✊"));
        all.Add((":punch:", "👊"));
        all.Add((":handshake:", "🤝"));
        all.Add((":metal:", "🤘"));
        all.Add((":horns:", "🤘"));
        all.Add((":v:", "✌️"));
        all.Add((":peace:", "✌️"));
        all.Add((":vulcan:", "🖖"));
        all.Add((":writing:", "✍️"));
        all.Add((":nail:", "💅"));
        all.Add((":lips:", "💋"));
        all.Add((":kiss:", "💋"));
        all.Add((":tongue:", "👅"));
        all.Add((":ear:", "👂"));
        all.Add((":nose:", "👃"));
        all.Add((":brain:", "🧠"));
        all.Add((":bone:", "🦴"));
        all.Add((":tooth:", "🦷"));
        all.Add((":leg:", "🦵"));
        all.Add((":foot:", "🦶"));
        all.Add((":baby:", "👶"));
        all.Add((":boy:", "👦"));
        all.Add((":girl:", "👧"));
        all.Add((":man:", "👨"));
        all.Add((":woman:", "👩"));
        all.Add((":old_man:", "👴"));
        all.Add((":old_woman:", "👵"));
        all.Add((":cop:", "👮"));
        all.Add((":santa:", "🎅"));
        all.Add((":wizard:", "🧙"));
        all.Add((":fairy:", "🧚"));
        all.Add((":vampire:", "🧛"));
        all.Add((":zombie:", "🧟"));
        all.Add((":mermaid:", "🧜"));
        all.Add((":monkey:", "🐵"));
        all.Add((":gorilla:", "🦍"));
        all.Add((":horse:", "🐴"));
        all.Add((":unicorn:", "🦄"));
        all.Add((":pig:", "🐷"));
        all.Add((":mouse:", "🐭"));
        all.Add((":rabbit:", "🐰"));
        all.Add((":fox:", "🦊"));
        all.Add((":bear:", "🐻"));
        all.Add((":panda:", "🐼"));
        all.Add((":koala:", "🐨"));
        all.Add((":tiger:", "🐯"));
        all.Add((":lion:", "🦁"));
        all.Add((":cow:", "🐮"));
        all.Add((":frog:", "🐸"));
        all.Add((":chicken:", "🐔"));
        all.Add((":penguin:", "🐧"));
        all.Add((":bird:", "🐦"));
        all.Add((":eagle:", "🦅"));
        all.Add((":duck:", "🦆"));
        all.Add((":owl:", "🦉"));
        all.Add((":bat:", "🦇"));
        all.Add((":shark:", "🦈"));
        all.Add((":whale:", "🐳"));
        all.Add((":dolphin:", "🐬"));
        all.Add((":fish:", "🐟"));
        all.Add((":octopus:", "🐙"));
        all.Add((":snail:", "🐌"));
        all.Add((":bug:", "🐛"));
        all.Add((":ant:", "🐜"));
        all.Add((":spider:", "🕷️"));
        all.Add((":scorpion:", "🦂"));
        all.Add((":turtle:", "🐢"));
        all.Add((":snake:", "🐍"));
        all.Add((":dragon:", "🐉"));
        all.Add((":dino:", "🦖"));
        all.Add((":cactus:", "🌵"));
        all.Add((":tree:", "🌳"));
        all.Add((":palm:", "🌴"));
        all.Add((":leaf:", "🍃"));
        all.Add((":flower:", "🌸"));
        all.Add((":rose:", "🌹"));
        all.Add((":tulip:", "🌷"));
        all.Add((":sunflower:", "🌻"));
        all.Add((":apple:", "🍎"));
        all.Add((":orange:", "🍊"));
        all.Add((":lemon:", "🍋"));
        all.Add((":banana:", "🍌"));
        all.Add((":watermelon:", "🍉"));
        all.Add((":grape:", "🍇"));
        all.Add((":strawberry:", "🍓"));
        all.Add((":cherry:", "🍒"));
        all.Add((":peach:", "🍑"));
        all.Add((":mango:", "🥭"));
        all.Add((":pineapple:", "🍍"));
        all.Add((":coconut:", "🥥"));
        all.Add((":avocado:", "🥑"));
        all.Add((":eggplant:", "🍆"));
        all.Add((":potato:", "🥔"));
        all.Add((":carrot:", "🥕"));
        all.Add((":corn:", "🌽"));
        all.Add((":pepper:", "🌶️"));
        all.Add((":cucumber:", "🥒"));
        all.Add((":broccoli:", "🥦"));
        all.Add((":mushroom:", "🍄"));
        all.Add((":bread:", "🍞"));
        all.Add((":cheese:", "🧀"));
        all.Add((":egg:", "🥚"));
        all.Add((":bacon:", "🥓"));
        all.Add((":burger:", "🍔"));
        all.Add((":fries:", "🍟"));
        all.Add((":hotdog:", "🌭"));
        all.Add((":taco:", "🌮"));
        all.Add((":burrito:", "🌯"));
        all.Add((":sushi:", "🍣"));
        all.Add((":ramen:", "🍜"));
        all.Add((":spaghetti:", "🍝"));
        all.Add((":curry:", "🍛"));
        all.Add((":ice_cream:", "🍦"));
        all.Add((":donut:", "🍩"));
        all.Add((":cookie:", "🍪"));
        all.Add((":chocolate:", "🍫"));
        all.Add((":candy:", "🍬"));
        all.Add((":popcorn:", "🍿"));
        all.Add((":tea:", "🍵"));
        all.Add((":wine:", "🍷"));
        all.Add((":cocktail:", "🍸"));
        all.Add((":champagne:", "🍾"));
        all.Add((":soccer:", "⚽"));
        all.Add((":basketball:", "🏀"));
        all.Add((":football:", "🏈"));
        all.Add((":baseball:", "⚾"));
        all.Add((":tennis:", "🎾"));
        all.Add((":golf:", "⛳"));
        all.Add((":trophy:", "🏆"));
        all.Add((":medal:", "🏅"));
        all.Add((":guitar:", "🎸"));
        all.Add((":piano:", "🎹"));
        all.Add((":drum:", "🥁"));
        all.Add((":mic:", "🎤"));
        all.Add((":headphones:", "🎧"));
        all.Add((":movie:", "🎬"));
        all.Add((":tv:", "📺"));
        all.Add((":camera:", "📷"));
        all.Add((":phone:", "📱"));
        all.Add((":computer:", "💻"));
        all.Add((":keyboard:", "⌨️"));
        all.Add((":mouse_computer:", "🖱️"));
        all.Add((":printer:", "🖨️"));
        all.Add((":cd:", "💿"));
        all.Add((":dvd:", "📀"));
        all.Add((":floppy:", "💾"));
        all.Add((":battery:", "🔋"));
        all.Add((":bulb:", "💡"));
        all.Add((":flashlight:", "🔦"));
        all.Add((":candle:", "🕯️"));
        all.Add((":bomb:", "💣"));
        all.Add((":gun:", "🔫"));
        all.Add((":knife:", "🔪"));
        all.Add((":hammer:", "🔨"));
        all.Add((":wrench:", "🔧"));
        all.Add((":gear:", "⚙️"));
        all.Add((":lock:", "🔒"));
        all.Add((":unlock:", "🔓"));
        all.Add((":key:", "🔑"));
        all.Add((":magnet:", "🧲"));
        all.Add((":bell:", "🔔"));
        all.Add((":mailbox:", "📬"));
        all.Add((":package:", "📦"));
        all.Add((":book:", "📖"));
        all.Add((":bookmark:", "🔖"));
        all.Add((":pencil:", "✏️"));
        all.Add((":pen:", "🖊️"));
        all.Add((":scissors:", "✂️"));
        all.Add((":paperclip:", "📎"));
        all.Add((":pushpin:", "📌"));
        all.Add((":calendar:", "📅"));
        all.Add((":chart:", "📊"));
        all.Add((":money_bag:", "💰"));
        all.Add((":dollar:", "💵"));
        all.Add((":credit_card:", "💳"));
        all.Add((":gem:", "💎"));
        all.Add((":ring:", "💍"));
        all.Add((":crown:", "👑"));
        all.Add((":hat:", "🎩"));
        all.Add((":glasses:", "👓"));
        all.Add((":tie:", "👔"));
        all.Add((":shirt:", "👕"));
        all.Add((":jeans:", "👖"));
        all.Add((":dress:", "👗"));
        all.Add((":bikini:", "👙"));
        all.Add((":shoe:", "👟"));
        all.Add((":boot:", "👢"));
        all.Add((":bag:", "👜"));
        all.Add((":umbrella:", "☂️"));
        all.Add((":rainbow:", "🌈"));
        all.Add((":lightning:", "⚡"));
        all.Add((":tornado:", "🌪️"));
        all.Add((":wave_water:", "🌊"));
        all.Add((":earth:", "🌍"));
        all.Add((":globe:", "🌐"));
        all.Add((":map:", "🗺️"));
        all.Add((":house:", "🏠"));
        all.Add((":office:", "🏢"));
        all.Add((":hospital:", "🏥"));
        all.Add((":bank:", "🏦"));
        all.Add((":hotel:", "🏨"));
        all.Add((":church:", "⛪"));
        all.Add((":castle:", "🏰"));
        all.Add((":tent:", "⛺"));
        all.Add((":car:", "🚗"));
        all.Add((":taxi:", "🚕"));
        all.Add((":bus:", "🚌"));
        all.Add((":truck:", "🚚"));
        all.Add((":bike:", "🚲"));
        all.Add((":motorcycle:", "🏍️"));
        all.Add((":train:", "🚆"));
        all.Add((":plane:", "✈️"));
        all.Add((":helicopter:", "🚁"));
        all.Add((":ship:", "🚢"));
        all.Add((":anchor:", "⚓"));
        all.Add((":fuel:", "⛽"));
        all.Add((":stoplight:", "🚦"));
        all.Add((":construction:", "🚧"));
        all.Add((":hourglass:", "⏳"));
        all.Add((":watch:", "⌚"));
        all.Add((":alarm:", "⏰"));
        all.Add((":timer:", "⏱️"));
        all.Add((":stopwatch:", "⏱️"));
        all.Add((":infinity:", "♾️"));
        all.Add((":recycle:", "♻️"));
        all.Add((":radioactive:", "☢️"));
        all.Add((":biohazard:", "☣️"));
        all.Add((":peace_sign:", "☮️"));
        all.Add((":yin_yang:", "☯️"));
        all.Add((":cross:", "✝️"));
        all.Add((":om:", "🕉️"));
        all.Add((":wheel:", "☸️"));
        all.Add((":atom:", "⚛️"));
        all.Add((":no_entry:", "⛔"));
        all.Add((":prohibited:", "🚫"));
        all.Add((":ok_hand:", "👌"));
        all.Add((":sos:", "🆘"));
        all.Add((":new:", "🆕"));
        all.Add((":free:", "🆓"));
        all.Add((":up:", "🆙"));
        all.Add((":cool_sign:", "🆒"));
        all.Add((":one:", "1️⃣"));
        all.Add((":two:", "2️⃣"));
        all.Add((":three:", "3️⃣"));
        all.Add((":four:", "4️⃣"));
        all.Add((":five:", "5️⃣"));
        all.Add((":six:", "6️⃣"));
        all.Add((":seven:", "7️⃣"));
        all.Add((":eight:", "8️⃣"));
        all.Add((":nine:", "9️⃣"));
        all.Add((":ten:", "🔟"));
        all.Add((":zero:", "0️⃣"));
        all.Add((":hash:", "#️⃣"));
        all.Add((":asterisk:", "*️⃣"));
        all.Add((":eject:", "⏏️"));
        all.Add((":play:", "▶️"));
        all.Add((":pause:", "⏸️"));
        all.Add((":stop_button:", "⏹️"));
        all.Add((":record:", "⏺️"));
        all.Add((":next:", "⏭️"));
        all.Add((":previous:", "⏮️"));
        all.Add((":fast_forward:", "⏩"));
        all.Add((":rewind:", "⏪"));
        all.Add((":shuffle:", "🔀"));
        all.Add((":repeat:", "🔁"));
        all.Add((":repeat_one:", "🔂"));
        all.Add((":speaker:", "🔊"));
        all.Add((":mute:", "🔇"));
        all.Add((":mega:", "📣"));
        all.Add((":loudspeaker:", "📢"));

        // Sort by length descending so longer matches are checked first
        Replacements = all.OrderByDescending(r => r.Item1.Length).ToArray();
    }

    /// <summary>
    /// Attempts to find and replace an emoji shortcode or emoticon at the end of the text.
    /// Returns null if no valid shortcode/emoticon found.
    /// </summary>
    public static (string newText, int cursorPosition)? TryReplaceShortcode(string text, int cursorPosition)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Check each possible replacement
        foreach (var (pattern, emoji) in Replacements)
        {
            // Check if text ends with this pattern (case-insensitive for shortcodes)
            if (!EndsWith(text, pattern)) continue;

            // Calculate where the pattern starts
            var startIndex = text.Length - pattern.Length;

            // Check if pattern is at start of string OR preceded by whitespace
            if (startIndex > 0 && !char.IsWhiteSpace(text[startIndex - 1])) continue;

            // Found a match! Build the replacement
            var newText = text.Substring(0, startIndex) + emoji;
            var newCursorPosition = newText.Length;

            return (newText, newCursorPosition);
        }

        return null;
    }

    private static bool EndsWith(string text, string pattern)
    {
        if (text.Length < pattern.Length) return false;

        // For :word: shortcodes, do case-insensitive comparison
        if (pattern.StartsWith(':') && pattern.EndsWith(':') && pattern.Length > 2)
        {
            return text.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // For emoticons, do exact match
        return text.EndsWith(pattern, StringComparison.Ordinal);
    }
}
