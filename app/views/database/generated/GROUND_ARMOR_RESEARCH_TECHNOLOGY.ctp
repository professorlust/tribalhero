<?php
                // Generated by DatabaseGenerator program on 9/20/2010 10:54:22 PM
	            $techKey = 'GROUND_ARMOR_RESEARCH_TECHNOLOGY';
	            $techName = 'Ground Unit Defence';
	
	            $trainedBy = array('name' => 'Armory', 'key' => 'ARMORY_STRUCTURE', 'level' => '1');
	
	            // Levels array should contain:
	            $levels = array(
		            array('description' => 'Increases the armor quality of all ground units by 5%.', 'time' => '90000', 'gold' => 0, 'crop' => 500, 'iron' => 0, 'labor' => 0, 'wood' => 500, 'requirements' => array()),
array('description' => 'Increases the armor quality of all ground units by 10%.', 'time' => '108000', 'gold' => 250, 'crop' => 1000, 'iron' => 0, 'labor' => 0, 'wood' => 1000, 'requirements' => array()),
array('description' => 'Increases the armor quality of all ground units by 15%.', 'time' => '126000', 'gold' => 500, 'crop' => 2000, 'iron' => 0, 'labor' => 0, 'wood' => 2000, 'requirements' => array()),
array('description' => 'Increases the armor quality of all ground units by 20%.', 'time' => '144000', 'gold' => 1000, 'crop' => 4000, 'iron' => 0, 'labor' => 0, 'wood' => 4000, 'requirements' => array()),
array('description' => 'Increases the armor quality of all ground units by 25%.', 'time' => '162000', 'gold' => 2000, 'crop' => 8000, 'iron' => 0, 'labor' => 0, 'wood' => 8000, 'requirements' => array()),

	            );

                include '/../technology_view.ctp';
            