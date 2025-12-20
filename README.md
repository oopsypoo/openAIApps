# openAIApps
**2025-11-12 1745** Added Video
 - Haven't worked on this project for a while so I made use of OpenAi. Very efficient...
 - Videocreation tool by OpenAI. Using the basic calls, ++ reference image. I does the job
 - Did not implement the remix-module. Maybe later
 - Dalle-E is not really Dall-E anymore since I hardcoded in the model-name (gpt-image-1). There are restrictions, but there are restrictions on every image-model (they are different)
 - 
   
**2024-05-20 1651** Added Vision
 - Added OpenAI Vision using Azure OpenAI API
 - Reason for this was that I had problems-/or it was confusing using JSON-format/structure. Couldn't make it work, so I shifted to Nuget->Azure.AI.OpenAI v1.0.0-beta.17
 - 

**2023-11-10 0810** Did minor changes/tidying up a little
 - All references to GPT3.5 Turbo is removed. All objects/classes have been reduces to just GPT. The sourcefile too.
 - The tab that used to have the header GPT3.5Turbo has now the name of the model used in the chat.

**2023-11-07** First commit of this hobby-project. Using WPF, OpenAI API. 
On this date there was a couple of updates from the OpenAI community regarding GPT 4 Turbo. And a bunch of other stuff. New to me so I thought I'd try it out and therefore I commited _this_ _stuff_ to Github.
Two models available in the **GPT4 Turbo** family at this time:
1. gpt-4-1106-preview - works "_out of the box_" in my code
                        _The latest GPT-4 model with improved instruction following, JSON mode, reproducible outputs, parallel function calling, and more. Returns a maximum of 4,096 output tokens. This preview model is not yet suited for production traffic__
2. gpt-4-vision-preview - this can be interesting to try out.
                          _Ability to understand images, in addition to all other GPT-4 Turbo capabilties. Returns a maximum of 4,096 output tokens. This is a preview model version and not suited yet for production traffic_

**DALL-E**
DALL-E v3 - I will see if I can update to this version.
Updated to version 3, But now I can't use EDIT or VARIATIONS..but I can see that the pics are "good".

**Assistant API**
Haven't tried yet. Can be interesting.

**TTS**
Will not update or do more here for the time being. I've allready tried using TTS via "Microsoft.CognitiveServices.Speech". I think this one is cooler. More voices/dialects from all over the world. Narrowed it down to English, Norwegian and Tagalog.
But if I'm right OpenAI offers a way so that you do not have to use this. Which is good. See source SpeechSynthesis.cs.

**GPTs**
Somehing to look into in the future? See their quotes below:
_We launched a new feature called GPTs. GPTs combine instructions, data, and capabilities into a customized version of ChatGPT_
_In addition to the capabilities built by OpenAI such as DALL·E or Advanced Data Analysis, GPTs can call developer-defined actions as well. GPTs let developers control a larger portion of experience. We purposefully architected plugins and actions very similarly, and it takes only a few minutes to turn an existing plugin into an action_
