# SeprrAPI Azure Function
This repository contains a repo of SeprrAPI azure function which updates user with Septa regional rail next three trains arriving from specific source to destination.

## Branches
All new development is done on the dev branch. More stable versions of the samples can be found on the master branch.

## Integration
### [Slack Support] - 
Currently it supports Slack client only. When a channel member slacks a formatted message, the outgoing webhook will send an HTTP GET request notification to the Webhook URL with the details to receive formatted response from azure function.

### [Slack Integration Setting] 
  
  1. Add Outgoing WebHooks : Add new outgoing webhook to slack to communicate with azure function via trigger

     ![Image of AddWebhook](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/AddWebHookApp.png?raw=true)
      
  2. Configure Azure Function : Set the Url as copied from azure portal where fucntion is deployed 
    
     ![Image of Webhook](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/ConfigureWebHook.png?raw=true)
 
  3. Configure Slack Trigger : This keyword will be used as a trigger from slack app to invoke azure function via webhook
    
     ![Image of Trigger](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/ConfigureTrigger.png?raw=true)
      
  4. Request azure function from slack : Channel members can request next available trains from slack using the trigger from configured 
     channel to view the next three available trains on the specific route.
    
     ![Image of Payload](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/SlackPayload.png?raw=true)
    
  5. Error result : Error response received from azure function
  
     ![Image of Error](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/error.png?raw=true)
  
## Building & Running
Download the dev branch, build and deploy it to the azure portal. Copy the azure function url and configure slack to start using the functionality,

## Further Development
Whatsapp Webhooks Integration
