/**
* ...
* @author Default
* @version 0.1
*/

package src.Util {

	public class StringHelper {
		public function StringHelper() {
		}

		public static function replace(str:String, oldSubStr:String, newSubStr:String):String {
			return str.split(oldSubStr).join(newSubStr);
		}

		public static function trim(str:String, char:String = ' '):String {
			return trimBack(trimFront(str, char), char);
		}

		public static function trimFront(str:String, char:String):String {
			char = stringToCharacter(char);
			if (str.charAt(0) == char) {
				str = trimFront(str.substring(1), char);
			}
			return str;
		}
		
		public static function truncate(str:String, maxLength: int = 150, ending: String = "..."): String {
			if (str.length + ending.length <= maxLength) 
				return str;
				
			return str.substr(0, maxLength) + ending;
		}

		public static function trimBack(str:String, char:String):String {
			char = stringToCharacter(char);
			if (str.charAt(str.length - 1) == char) {
				str = trimBack(str.substring(0, str.length - 1), char);
			}
			return str;
		}

		public static function stringToCharacter(str:String):String {
			if (str.length == 1) {
				return str;
			}
			return str.slice(0, 1);
		}
		
		public static function numberInWords(value: int) : String {
			var numbers: Array = new Array("zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten");
			if (value >= numbers.length || value < 0) return value.toString();
			return numbers[value];
		}
		
		public static function makePlural(value: int, singular: String, plural: String, zero: String = "") : String {
			if (zero != "" && value == 0)
			return zero;

			return value > 1 ? plural : singular;
		}

		public static function firstToUpper(word: String) : String{
			var firstLetter: String = word.substring(1, 0);
			var restOfWord: String = word.substring(1);
			return firstLetter.toUpperCase() + restOfWord;
		}		
		
		public static function wordsToUpper(s:String):String {			
			var ret: * = s.replace(/ [a-z]/g, function (m:String, ... rest):String {
				return m.toUpperCase();
			});
			
			return ret;
		}
	}
	
}