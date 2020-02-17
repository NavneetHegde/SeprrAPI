# SeprrAPI Azure Function
This repository contains a repo of SeprrAPI azure function which updates user with Septa regional rail next three trains arriving from specific source to destination.

## Branches
All new development is done on the dev branch. More stable versions of the samples can be found on the master branch.

## Integration
* [Slack Support] - Currently it supports Slack client only. Members of slack channel can request api with specific request pattern to read the formatted azure function response result.

* [Slack Integration] 
  
  1.  Add Outgoing WebHooks : Add new outgoing webhook to slack to communicate with azure function via trigger

     ![Image of Yaktocat](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/AddWebHookApp.png?raw=true)
      
  2.  Configure Azure Function : Set the Url as copied from azure portal where fucntion is deployed 
    
     ![Image of Yaktocat](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/ConfigureWebHook.png?raw=true)
 
  3.  Configure Slack Trigger : This word will be used as a trigger from slack app to invoke azure function via webhook
    
     ![Image of Yaktocat](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/ConfigureTrigger.png?raw=true)
      
  4.  Request azure function from slack : Channel members can request next available trains from slack using the trigger from configured 
      channel to view the next three available trains on the specific route.
    
     ![Image of Yaktocat](https://github.com/NavneetHegde/SeprrAPI/blob/dev/SeprrAPI/Images/SlackPayload.png?raw=true)
    
    
    
    

  
  


## Building & Running

See the Readmes for each sample for instructions on how to build and run.
