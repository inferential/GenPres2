namespace Informedica.OpenAI.Lib


module Prompts =

    open Newtonsoft.Json

    let tasks = """
You are a world-class prompt engineering assistant. Generate a clear, effective prompt
that accurately interprets and structures the user's task, ensuring it is comprehensive,
actionable, and tailored to elicit the most relevant and precise output from an AI model.
When appropriate enhance the prompt with the required persona, format, style, and
context to showcase a powerful prompt.
"""

    let taskPrompt task = $"""
# Task

{task}
"""


    let assistentAsk = """
You are a world-class AI assistant. Your communication is brief and concise.
You're precise and answer only when you're confident in the high quality of your answer.
"""

    let createAsk question = $"""
# Question:

{question}
"""


    let extractData = """
You are a world-class expert for function-calling and data extraction.
Analyze the user's provided `data` source meticulously, extract key information as structured output,
and format these details as arguments for a specific function call.
Ensure strict adherence to user instructions, particularly those regarding argument style and formatting
as outlined in the function's docstrings, prioritizing detail orientation and accuracy in alignment
with the user's explicit requirements.
"""


    let createExtractData data = $"""
# Data

{data}
"""


    module System =

        let systemDoseQuantityExpert text = $"""
You are an expert on medication prescribing, preparation and administration. You will give
exact answers. If there is no possible answer return an empty string.
You will be asked to extract structured information from the following text between ''':

'''{text}'''

ONLY respond if the response is actually present in the text. If the response cannot be extracted
respond with an empty string for a textfield or zero for a number field.

Respond in JSON
"""


    module User =


        let respondInJson txt = $"{txt}\nRespond in JSON"


        let addZeroCase zero s =
            let zero =
                $"{zero |> JsonConvert.SerializeObject}"
            $"{s}\nIf extraction is not possible return: \"{zero}\""
            |> respondInJson


        let substanceUnitText = """
Use the provided schema to extract the unit of measurement for the medication substance (substance unit)
from the medication dosage information contained in the text.

Use schema: { substanceUnit: string }

Here are some examples and expected output:
 - For "mg/kg/dag", return: "{ "substanceUnit": "mg" }"
 - For "g/m2/dag", return: "{ "substanceUnit": "g" }"
 - For "IE/m2", return: "{ "substanceUnit": "IE" }"
 - For "mg/2 dagen", return: "{ "substanceUnit": "mg" }"
"""

        let adjustUnitText zero =
            let s = """
Use the provided schema to extract the unit of measurement by which a medication dose is adjusted,
such as patient weight or body surface area, from the medication dosage information contained in the text.

Use schema : { adjustUnit: string }

Here are some examples and expected output:
- For "mg/kg/dag", return: "{ "adjustUnit": "kg" }"
- For "mg/kg", return: "{ "adjustUnit": "kg" }"
- For "mg/m2/dag", return: "{ "adjustUnit": "m2" }"
- For "mg/m2", return: "{ "adjustUnit": "m2" }"
"""
            s |> addZeroCase zero

        let timeUnitText zero =
            let s = """
Use the provided schema to extract the time unit from the medication dosage information contained in the text.
The timeunit is the unit by which the frequency of administration is measured.

Use schema : { timeUnit: string }

Here are some examples and expected output:
- For "mg/kg/dag", return: "{ "timeUnit": "dag" }"
- For "mg/kg", return: "{ "timeUnit": "" }"
- For "mg/m2/week", return: "{ "timeUnit": "week" }"
- For "mg/2 dagen", return: "{ "timeUnit": "2 dagen" }"
- For "per week", return "{ "timeUnit": "week" }"
- For "per dag", return "{ "timeUnit": "week" }"
"""
            s |> addZeroCase zero


        let frequencyText timeUnit zero =
            let s = """
Use the provided schema to extract the frequencies by which the drug can be administered.
The frequencies field in the schema should be an array of integers.

Use schema : { frequencies: []; timeUnit: string }
"""
            $"{s}\nNote that the frequencies should be possible times of administrations per {timeUnit}"
            |> addZeroCase zero


        let minMaxDoseText (substanceUnit: string) (adjustUnit: string option) (timeUnit: string option) =
            let regex =
                let secUnit, thirdUnit =
                    match adjustUnit, timeUnit with
                    | None, None -> "keer|dosis", "keer|dosis"
                    | Some au, Some tu ->
                        $"{au}|{tu}|keer|dosis",
                        $"{au}|{tu}|keer|dosis"
                    | None, Some tu ->
                        $"{tu}|keer|dosis",
                        $"{tu}"
                    | Some au, None ->
                        $"{au}|keer|dosis",
                        $"{au}|keer|dosis"

                $"(\d+(\.\d+)?\s?({substanceUnit})(\/({secUnit}))?(\s?\/\s?({thirdUnit}))?\s)"

            """
Use the provided schema to extract all mentioned dose quantities.
A quantity should match the regular expresion between the ticks '[REGEX]'

Return dose quantities as a list of JSON
Use schema :
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "quantities": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "minQty": {
            "type": "number",
            "minimum": 0
          },
          "maxQty": {
            "type": "number",
            "minimum": 0
          },
          "unit": {
            "type": "string",
            "enum": ["mg", "mg/kg"]
          }
        },
        "required": ["minQty", "maxQty", "unit"]
      }
    }
  },
  "required": ["quantities"]
}

Respond in JSON""".Replace("[REGEX]", regex)
